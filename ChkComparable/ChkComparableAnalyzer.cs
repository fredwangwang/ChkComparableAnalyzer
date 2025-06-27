using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ChkComparable
{
    public enum ComparabilityResult
    {
        Comparable,
        NotComparable,
        PotentiallyNotComparable
    }

    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ChkComparableAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "CMPABLE";
        public const string WarningDiagnosticId = "CMPABLE_WARN";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString WarningTitle = new LocalizableResourceString(nameof(Resources.WarningAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString WarningMessageFormat = new LocalizableResourceString(nameof(Resources.WarningAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString WarningDescription = new LocalizableResourceString(nameof(Resources.WarningAnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);
        private static readonly DiagnosticDescriptor WarningRule = new DiagnosticDescriptor(WarningDiagnosticId, WarningTitle, WarningMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: WarningDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule, WarningRule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Register syntax node action to analyze method invocations
            context.RegisterSyntaxNodeAction(AnalyzeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeMethodInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;
            
            // Check if this is a method invocation
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return;

            // Check if the method name is OrderBy, OrderByDescending, ThenBy, or ThenByDescending
            string methodName = memberAccess.Name.Identifier.ValueText;
            if (methodName != "OrderBy" && methodName != "OrderByDescending" && 
                methodName != "ThenBy" && methodName != "ThenByDescending")
                return;

            // Get the semantic model to analyze the method call
            var semanticModel = context.SemanticModel;
            var methodSymbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            
            if (methodSymbol == null)
                return;

            // Check if this is a LINQ OrderBy/OrderByDescending method
            if (!IsLinqOrderByMethod(methodSymbol))
                return;

            // Get the key selector argument (the lambda expression that selects the key)
            if (invocation.ArgumentList?.Arguments.Count < 1)
                return;

            var keySelectorArgument = invocation.ArgumentList.Arguments[0];
            
            // Check if a custom comparer is provided (second argument)
            bool hasCustomComparer = invocation.ArgumentList.Arguments.Count > 1;
            
            if (hasCustomComparer)
            {
                // If a custom comparer is provided, no need to check the key type
                return;
            }

            // Analyze the key selector to determine the key type
            var keyType = GetKeyTypeFromSelector(semanticModel, keySelectorArgument.Expression);
            
            if (keyType == null)
                return;

            // Check if the key type is comparable
            var comparabilityResult = CheckComparability(keyType);
            
            if (comparabilityResult == ComparabilityResult.NotComparable)
            {
                var diagnostic = Diagnostic.Create(Rule, keySelectorArgument.GetLocation(), keyType.Name);
                context.ReportDiagnostic(diagnostic);
            }
            else if (comparabilityResult == ComparabilityResult.PotentiallyNotComparable)
            {
                var diagnostic = Diagnostic.Create(WarningRule, keySelectorArgument.GetLocation(), keyType.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool IsLinqOrderByMethod(IMethodSymbol methodSymbol)
        {
            // Check if this is a LINQ OrderBy/OrderByDescending/ThenBy/ThenByDescending method
            // LINQ methods are extension methods on IEnumerable<T>
            if (!methodSymbol.IsExtensionMethod)
                return false;

            // Check if the method is from System.Linq.Enumerable
            var containingType = methodSymbol.ContainingType;
            if (containingType?.ContainingNamespace?.ToString() != "System.Linq")
                return false;

            // Check if the method name matches
            return methodSymbol.Name == "OrderBy" || methodSymbol.Name == "OrderByDescending" ||
                   methodSymbol.Name == "ThenBy" || methodSymbol.Name == "ThenByDescending";
        }

        private static ITypeSymbol GetKeyTypeFromSelector(SemanticModel semanticModel, ExpressionSyntax keySelector)
        {
            // Handle lambda expressions
            if (keySelector is LambdaExpressionSyntax lambda)
            {
                // For simple lambdas, get the type of the body expression
                if (lambda.Body is ExpressionSyntax exprBody)
                {
                    var typeInfo = semanticModel.GetTypeInfo(exprBody);
                    return typeInfo.Type;
                }
                // For block-bodied lambdas, not supported in OrderBy key selectors
            }
            
            // Handle delegate types (like Func<T, string>)
            var delegateTypeInfo = semanticModel.GetTypeInfo(keySelector);
            if (delegateTypeInfo.Type is INamedTypeSymbol namedType && 
                namedType.OriginalDefinition.ToString().StartsWith("System.Func<"))
            {
                // For Func<T1, T2, ..., TResult>, the last type argument is the return type
                if (namedType.TypeArguments.Length > 0)
                {
                    return namedType.TypeArguments[namedType.TypeArguments.Length - 1];
                }
            }
            
            // Handle simple expressions
            return delegateTypeInfo.Type;
        }

        private static bool IsComparableType(ITypeSymbol type)
        {
            return CheckComparability(type) == ComparabilityResult.Comparable;
        }

        private static bool IsPrimitiveType(ITypeSymbol type)
        {
            if (type == null)
                return false;

            // Check for primitive types that are naturally comparable
            switch (type.SpecialType)
            {
                case SpecialType.System_Boolean:
                case SpecialType.System_Char:
                case SpecialType.System_SByte:
                case SpecialType.System_Byte:
                case SpecialType.System_Int16:
                case SpecialType.System_UInt16:
                case SpecialType.System_Int32:
                case SpecialType.System_UInt32:
                case SpecialType.System_Int64:
                case SpecialType.System_UInt64:
                case SpecialType.System_Decimal:
                case SpecialType.System_Single:
                case SpecialType.System_Double:
                case SpecialType.System_String:
                case SpecialType.System_DateTime:
                    return true;
                default:
                    return false;
            }
        }

        private static bool ImplementsIComparableT(ITypeSymbol type)
        {
            if (type == null)
                return false;

            // Check if the type implements IComparable<T> for any T
            var hasComparableT = type.Interfaces.Any(i => 
                i.OriginalDefinition.ToString() == "System.IComparable<T>" &&
                i.TypeArguments.Length == 1);

            return hasComparableT;
        }

        private static bool ImplementsIComparable(ITypeSymbol type)
        {
            if (type == null)
                return false;

            // Check if the type implements IComparable
            return type.Interfaces.Any(i => i.ToString() == "System.IComparable");
        }

        private static ComparabilityResult IsGenericTypeParameterComparable(ITypeParameterSymbol typeParameter)
        {
            if (typeParameter == null)
                return ComparabilityResult.NotComparable;

            // 1. struct constraint: always comparable
            if (typeParameter.HasValueTypeConstraint)
                return ComparabilityResult.Comparable;

            // 2. Check constraint types (e.g., where T : SomeBaseClass, ISomeInterface)
            if (typeParameter.ConstraintTypes.Length > 0)
            {
                bool hasPotential = false;
                foreach (var constraintType in typeParameter.ConstraintTypes)
                {
                    var result = CheckComparability(constraintType);
                    if (result == ComparabilityResult.Comparable)
                        return ComparabilityResult.Comparable;
                    if (result == ComparabilityResult.PotentiallyNotComparable)
                        hasPotential = true;
                }
                return hasPotential ? ComparabilityResult.PotentiallyNotComparable : ComparabilityResult.NotComparable;
            }

            // 3. Only class constraint, no specific types
            if (typeParameter.HasReferenceTypeConstraint)
                return ComparabilityResult.PotentiallyNotComparable;

            // 4. No constraints at all
            return ComparabilityResult.PotentiallyNotComparable;
        }

        private static ComparabilityResult CheckComparability(ITypeSymbol type)
        {
            if (type == null)
                return ComparabilityResult.NotComparable;

            // Check for dynamic and object types - these are potentially not comparable at runtime
            if (type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic)
                return ComparabilityResult.PotentiallyNotComparable;

            // Check if it's a primitive type
            if (IsPrimitiveType(type))
                return ComparabilityResult.Comparable;

            // Check if it's an enum type (enums are comparable by default)
            if (type.TypeKind == TypeKind.Enum)
                return ComparabilityResult.Comparable;

            // Check if it's a generic type parameter and check its constraints
            if (type.TypeKind == TypeKind.TypeParameter)
            {
                return IsGenericTypeParameterComparable((ITypeParameterSymbol)type);
            }

            // Check if it implements IComparable<T>
            if (ImplementsIComparableT(type))
                return ComparabilityResult.Comparable;

            // Check if it implements IComparable
            if (ImplementsIComparable(type))
                return ComparabilityResult.Comparable;

            // Check if it's a nullable type and the underlying type is comparable
            if (type is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            {
                var underlyingType = namedType.TypeArguments[0];
                return CheckComparability(underlyingType);
            }

            // Recursively check base types
            if (type.BaseType != null && type.BaseType.SpecialType != SpecialType.System_Object)
            {
                var baseResult = CheckComparability(type.BaseType);
                if (baseResult != ComparabilityResult.NotComparable)
                    return baseResult;
            }

            return ComparabilityResult.NotComparable;
        }
    }
}
