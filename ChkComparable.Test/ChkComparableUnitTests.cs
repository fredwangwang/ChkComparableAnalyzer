using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS = ChkComparable.Test.CSharpAnalyzerVerifier<ChkComparable.ChkComparableAnalyzer>;

namespace ChkComparable.Test
{
    [TestClass]
    public class ChkComparableUnitTests
    {
        [TestMethod]
        public async Task TestEmptyCode_NoDiagnostic()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithComparableType_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result = list.OrderBy(p => p.Name); // string is comparable
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithNonComparableType_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result = list.OrderBy(p => p); // Person is not comparable
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            var expected = VerifyCS.Diagnostic(ChkComparable.ChkComparableAnalyzer.DiagnosticId)
                .WithSpan(13, 39, 13, 45)
                .WithArguments("Person");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestOrderByDescendingWithNonComparableType_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result = list.OrderByDescending(p => p); // Person is not comparable
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            var expected = VerifyCS.Diagnostic(ChkComparable.ChkComparableAnalyzer.DiagnosticId)
                .WithSpan(13, 49, 13, 55)
                .WithArguments("Person");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestOrderByWithCustomComparer_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result = list.OrderBy(p => p, new PersonComparer()); // Custom comparer provided
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }

    public class PersonComparer : IComparer<Person>
    {
        public int Compare(Person x, Person y)
        {
            return string.Compare(x?.Name, y?.Name);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithIComparableType_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<ComparablePerson>();
            var result = list.OrderBy(p => p); // ComparablePerson implements IComparable
        }
    }

    public class ComparablePerson : IComparable<ComparablePerson>
    {
        public string Name { get; set; }
        public int Age { get; set; }

        public int CompareTo(ComparablePerson other)
        {
            return string.Compare(Name, other?.Name);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithPrimitiveTypes_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result1 = list.OrderBy(p => p.Age); // int is comparable
            var result2 = list.OrderBy(p => p.Name); // string is comparable
            var result3 = list.OrderBy(p => p.Height); // double is comparable
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public double Height { get; set; }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithEnumType_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result = list.OrderBy(p => p.Status); // Status is an enum
        }
    }

    public enum PersonStatus
    {
        Active,
        Inactive,
        Pending
    }

    public class Person
    {
        public string Name { get; set; }
        public PersonStatus Status { get; set; }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithNullableTypes_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result1 = list.OrderBy(p => p.Age); // int? is comparable
            var result2 = list.OrderBy(p => p.Height); // double? is comparable
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int? Age { get; set; }
        public double? Height { get; set; }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithNonLinqMethod_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result = list.Where(p => p.Age > 18); // Not OrderBy, so no diagnostic
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithFuncDelegate_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            
            // Test with Func<T, string> delegate
            Func<Person, string> orderFunc = p => p.Name;
            var result1 = list.OrderBy(orderFunc); // Should not report diagnostic
            
            // Test with Func<T, int> delegate
            Func<Person, int> ageFunc = p => p.Age;
            var result2 = list.OrderBy(ageFunc); // Should not report diagnostic
            
            // Test with Func<T, DateTime> delegate
            Func<Person, DateTime> dateFunc = p => p.BirthDate;
            var result3 = list.OrderBy(dateFunc); // Should not report diagnostic
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public DateTime BirthDate { get; set; }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithFuncDelegateNonComparableType_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            
            // Test with Func<T, Person> delegate (Person is not comparable)
            Func<Person, Person> personFunc = p => p;
            var result = list.OrderBy(personFunc); // Should report diagnostic
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            var expected = VerifyCS.Diagnostic(ChkComparable.ChkComparableAnalyzer.DiagnosticId)
                .WithSpan(16, 39, 16, 49)
                .WithArguments("Person");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestOrderByWithGenericTypeParameterConstrainedToComparable_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod<T>(List<T> list) where T : ComparablePerson
        {
            var result = list.OrderBy(item => item); // T is constrained to ComparablePerson which implements IComparable
        }
    }

    public class ComparablePerson : IComparable<ComparablePerson>
    {
        public string Name { get; set; }
        public int Age { get; set; }

        public int CompareTo(ComparablePerson other)
        {
            return string.Compare(Name, other?.Name);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithGenericTypeParameterConstrainedToPrimitive_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod<T>(List<T> list) where T : struct
        {
            var result = list.OrderBy(item => item); // T is constrained to struct (value type), should be comparable
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithGenericTypeParameterConstrainedToNonComparable_Diagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod<T>(List<T> list) where T : Person
        {
            var result = list.OrderBy(item => item); // T is constrained to Person which doesn't implement IComparable
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            var expected = VerifyCS.Diagnostic(ChkComparable.ChkComparableAnalyzer.DiagnosticId)
                .WithSpan(12, 39, 12, 51)
                .WithArguments("T");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestOrderByWithGenericTypeParameterInheritanceChain_NoDiagnostic()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result = list.OrderBy(p => p.Name); // string is comparable
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task TestOrderByWithDynamicType_Warning()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result = list.OrderBy(p => GetDynamicValue(p)); // dynamic type - warning
        }

        private dynamic GetDynamicValue(Person p)
        {
            return p.Name;
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            var expected = VerifyCS.Diagnostic(ChkComparable.ChkComparableAnalyzer.WarningDiagnosticId)
                .WithSpan(13, 39, 13, 62)
                .WithArguments("dynamic");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestOrderByWithObjectType_Warning()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            var result = list.OrderBy(p => GetObjectValue(p)); // object type - warning
        }

        private object GetObjectValue(Person p)
        {
            return p.Name;
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            var expected = VerifyCS.Diagnostic(ChkComparable.ChkComparableAnalyzer.WarningDiagnosticId)
                .WithSpan(13, 39, 13, 61)
                .WithArguments("Object");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestOrderByWithFuncDynamic_Warning()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            Func<Person, dynamic> selector = p => p.Name;
            var result = list.OrderBy(selector); // Func<T, dynamic> - warning
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            var expected = VerifyCS.Diagnostic(ChkComparable.ChkComparableAnalyzer.WarningDiagnosticId)
                .WithSpan(14, 39, 14, 47)
                .WithArguments("dynamic");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task TestOrderByWithFuncObject_Warning()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {
            var list = new List<Person>();
            Func<Person, object> selector = p => p.Name;
            var result = list.OrderBy(selector); // Func<T, object> - warning
        }
    }

    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
    }
}";

            var expected = VerifyCS.Diagnostic(ChkComparable.ChkComparableAnalyzer.WarningDiagnosticId)
                .WithSpan(14, 39, 14, 47)
                .WithArguments("Object");

            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}
