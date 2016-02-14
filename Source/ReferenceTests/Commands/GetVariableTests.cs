﻿// Copyright (C) Pash Contributors. License: GPL/BSD. See https://github.com/Pash-Project/Pash/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using NUnit.Framework;

namespace ReferenceTests.Commands
{
    [TestFixture]
    public class GetVariableTests : ReferenceTestBase
    {
        [Test]
        public void ByName()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$foo = 'bar'",
                "(Get-Variable foo).Value"
            });

            Assert.AreEqual("bar" + Environment.NewLine, result);
        }

        [Test]
        public void NameParameter()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$foo = 'bar'",
                "(Get-Variable -name foo).Value"
            });

            Assert.AreEqual("bar" + Environment.NewLine, result);
        }

        [Test]
        public void NameIsCaseInsensitive()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$foo = 'bar'",
                "(Get-Variable -name FOO).Value"
            });

            Assert.AreEqual("bar" + Environment.NewLine, result);
        }

        [Test]
        public void GetTwoVariables()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$a = '1'",
                "$b = '2'",
                "Get-Variable 'a','b' | % { $_.value }"
            });

            Assert.AreEqual(NewlineJoin("1", "2"), result);
        }

        [Test]
        public void GetTwoVariablesUsingNamesPassedOnPipeline()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$a = '1'",
                "$b = '2'",
                "'a','b' | Get-Variable | % { $_.value }"
            });

            Assert.AreEqual(NewlineJoin("1", "2"), result);
        }

        [Test]
        public void NameAndValueOnlyParameter()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$foo = 'bar'",
                "Get-Variable foo -ValueOnly"
            });

            Assert.AreEqual("bar" + Environment.NewLine, result);
        }

        [Test]
        public void FunctionLocalScopeVariable()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$test = 'test-global'",
                "function foo { $test = 'test-local'; Get-Variable test -Scope local -valueonly; }",
                "foo"
            });

            Assert.AreEqual("test-local" + Environment.NewLine, result);
        }

        [Test]
        public void FunctionGlobalScopeVariable()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$test = 'test-global'",
                "function foo { $test = 'test-local'; Get-Variable test -Scope global -valueonly; }",
                "foo"
            });

            Assert.AreEqual("test-global" + Environment.NewLine, result);
        }

        [Test]
        public void NoParametersReturnsAllParameters()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$foo = 'bar'",
                "Get-Variable| % { $_.Value }"
            });

            StringAssert.Contains("bar" + Environment.NewLine, result);
        }

        [Test]
        public void NoNameAndValueOnlyReturnsAllParameterValues()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$foo = 'bar'",
                "Get-Variable -ValueOnly"
            });

            StringAssert.Contains("bar" + Environment.NewLine, result);
        }

        [Test]
        public void NoNameAndLocalScope()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$test = 'test-global'",
                "function foo { $test = 'test-local'; Get-Variable -Scope local; }",
                "foo | ? { $_.Name -eq 'test' } | % { $_.Value }"
            });

            Assert.AreEqual("test-local" + Environment.NewLine, result);
        }

        [Test]
        public void NoNameAndGlobalScope()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$test = 'test-global'",
                "function foo { $test = 'test-local'; Get-Variable -Scope global; }",
                "foo | ? { $_.Name -eq 'test' } | % { $_.Value }"
            });

            Assert.AreEqual("test-global" + Environment.NewLine, result);
        }

        [Test]
        public void FunctionNoScopeSpecifiedUsesLocalScope()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$test = 'test-global'",
                "function foo { $test = 'test-local'; Get-Variable test -valueonly; }",
                "foo"
            });

            Assert.AreEqual("test-local" + Environment.NewLine, result);
        }

        [Test]
        public void HostVariableName()
        {
            string result = ReferenceHost.Execute("(Get-Variable host).Name");

            Assert.AreEqual("Host" + Environment.NewLine, result);
        }

        [Test]
        public void IncludeTwoVariables()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$testabc = 'abc'",
                "$testbar = 'bar'",
                "$testfoo = 'foo'",
                "gv -Include testfoo,testbar -valueonly"
            });

            Assert.AreEqual(NewlineJoin("bar", "foo"), result);
        }

        [Test]
        public void ExcludeTwoVariables()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$testfoo = 'foo'",
                "$testbar = 'bar'",
                "$testabc = 'abc'",
                "gv -exclude testfoo,testbar -valueonly"
            });

            StringAssert.Contains("abc" + Environment.NewLine, result);
            StringAssert.DoesNotContain("foo", result);
            StringAssert.DoesNotContain("bar", result);
        }

        [Test]
        public void IncludeIsCaseInsensitive()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$testbar = 'bar'",
                "$testfoo = 'foo'",
                "Get-Variable -Include TESTFOO -valueonly"
            });

            Assert.AreEqual("foo" + Environment.NewLine, result);
        }

        [Test]
        public void ExcludeIsCaseInsensitive()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$testfoo = 'foo'",
                "$testbar = 'bar'",
                "Get-Variable -exclude TESTFOO -valueonly"
            });

            StringAssert.Contains("bar" + Environment.NewLine, result);
            StringAssert.DoesNotContain("foo", result);
        }

        [Test]
        public void NameAndIncludeSpecified()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$testfoo = 'foo'",
                "$testbar = 'bar'",
                "Get-Variable testfoo -include testbar -valueonly"
            });

            Assert.AreEqual(String.Empty, result);
        }

        [Test]
        public void NameAndExcludeSpecified()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$testfoo = 'foo'",
                "$testbar = 'bar'",
                "Get-Variable testfoo -exclude TESTFOO -valueonly"
            });

            Assert.AreEqual(String.Empty, result);
        }

        [Test]
        public void NameIsWildcard()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$testbar = 'bar'",
                "$testfoo = 'foo'",
                "Get-Variable test* -valueonly"
            });

            Assert.AreEqual(NewlineJoin("bar", "foo"), result);
        }

        [Test]
        public void NameIsWildcardAndLocalScope()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$test = 'test-global'",
                "function foo { $test = 'test-local'; Get-Variable test* -Scope local -valueonly; }",
                "foo"
            });

            Assert.AreEqual("test-local" + Environment.NewLine, result);
        }

        [Test]
        public void IncludeIsWildcard()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$testbar = 'bar'",
                "$testfoo = 'foo'",
                "Get-Variable -include TESTF* -valueonly"
            });

            Assert.AreEqual("foo" + Environment.NewLine, result);
        }

        [Test]
        public void ExcludeIsWildcard()
        {
            string result = ReferenceHost.Execute(new string[] {
                "$testabc = 'abc'",
                "$testbar = 'bar'",
                "$testfoo = 'foo'",
                "Get-Variable -include test* -exclude TESTA* -valueonly"
            });

            Assert.AreEqual(NewlineJoin("bar", "foo"), result);
        }

        [Test]
        public void UnknownNameCausesError()
        {
            Assert.Throws<ExecutionWithErrorsException>(delegate
            {
                ReferenceHost.Execute("Get-Variable unknownvariable");
            });

            ErrorRecord error = ReferenceHost.GetLastRawErrorRecords().Single();
            Assert.AreEqual("Cannot find a variable with name 'unknownvariable'.", error.Exception.Message);
            Assert.AreEqual("VariableNotFound,Microsoft.PowerShell.Commands.GetVariableCommand", error.FullyQualifiedErrorId);
            Assert.AreEqual("unknownvariable", error.TargetObject);
            Assert.IsInstanceOf<ItemNotFoundException>(error.Exception);
            Assert.AreEqual("Get-Variable", error.CategoryInfo.Activity);
            Assert.AreEqual(ErrorCategory.ObjectNotFound, error.CategoryInfo.Category);
            Assert.AreEqual("ItemNotFoundException", error.CategoryInfo.Reason);
            Assert.AreEqual("unknownvariable", error.CategoryInfo.TargetName);
            Assert.AreEqual("String", error.CategoryInfo.TargetType);
        }
    }
}
