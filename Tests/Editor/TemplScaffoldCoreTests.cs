/*
 * Copyright (c) 2022 Willy Alberto Kuster
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
using NUnit.Framework;
using UnityEngine;

namespace Willykc.Templ.Editor.Tests
{
    using Mocks;
    using Scaffold;

    internal class TemplScaffoldCoreTests
    {
        private const string TestScaffoldPath =
            "Packages/com.willykc.templ/Tests/Editor/TestAssets~/TestScaffold.asset";
        private const string TestScaffoldTemplatePath =
            "Packages/com.willykc.templ/Tests/Editor/TestAssets~/TestScaffoldTemplate.sbn";
        private const string TestDynamicScaffoldPath =
            "Packages/com.willykc.templ/Tests/Editor/TestAssets~/TestDynamicScaffold.asset";
        private const string TestTreeTemplatePath =
            "Packages/com.willykc.templ/Tests/Editor/TestAssets~/TestTreeTemplate.sbn";
        private const string TestDynamicScaffoldWithEmptyTemplatePath =
            "Packages/com.willykc.templ/Tests/Editor/TestAssets~/" +
            "TestDynamicScaffoldWithEmptyTemplate.asset";
        private const string TestEmptyTreeTemplatePath =
            "Packages/com.willykc.templ/Tests/Editor/TestAssets~/TestEmptyTreeTemplate.sbn";
        private const string TestTargetPath = "Assets/Some/Path";
        private const string InputName = "roach";

        private static readonly string[] Elements = new[] { "one", "two" };

        private TemplScaffoldCore subject;
        private InputType testInput;
        private Object testSelection;
        private FileSystemMock fileSystemMock;
        private LoggerMock loggerMock;
        private EditorUtilityMock editorUtilityMock;
        private TemplateFunctionProviderMock templateFunctionProviderMock;
        private TemplScaffold testScaffold;
        private ScribanAsset testTreeTemplate;
        private TemplScaffold testDynamicScaffold;
        private ScribanAsset testEmptyTreeTemplate;
        private ScribanAsset testScaffoldTemplate;
        private TemplScaffold testDynamicScaffoldWithEmptyTemplate;

        [OneTimeSetUp]
        public void BeforeAll()
        {
            testScaffoldTemplate = TemplTestUtility
                .CreateTestAsset<ScribanAsset>(TestScaffoldTemplatePath, out _);
            testScaffold = TemplTestUtility.CreateTestAsset<TemplScaffold>(TestScaffoldPath, out _);
            testTreeTemplate = TemplTestUtility
                .CreateTestAsset<ScribanAsset>(TestTreeTemplatePath, out _);
            testDynamicScaffold = TemplTestUtility
                .CreateTestAsset<TemplScaffold>(TestDynamicScaffoldPath, out _);
            testEmptyTreeTemplate = TemplTestUtility
                .CreateTestAsset<ScribanAsset>(TestEmptyTreeTemplatePath, out _);
            testDynamicScaffoldWithEmptyTemplate = TemplTestUtility
                .CreateTestAsset<TemplScaffold>(TestDynamicScaffoldWithEmptyTemplatePath, out _);
        }

        [SetUp]
        public void BeforeEach()
        {
            subject = new TemplScaffoldCore(
                fileSystemMock = new FileSystemMock(),
                loggerMock = new LoggerMock(),
                editorUtilityMock = new EditorUtilityMock(),
                templateFunctionProviderMock = new TemplateFunctionProviderMock());

            testInput = new InputType()
            {
                name = InputName,
                elements = Elements,
                induce_runtime_error = false
            };

            testSelection = testScaffold;
        }

        [OneTimeTearDown]
        public void AfterAll()
        {
            TemplTestUtility.DeleteTestAsset(testScaffoldTemplate);
            TemplTestUtility.DeleteTestAsset(testScaffold);
            TemplTestUtility.DeleteTestAsset(testTreeTemplate);
            TemplTestUtility.DeleteTestAsset(testDynamicScaffold);
            TemplTestUtility.DeleteTestAsset(testEmptyTreeTemplate);
            TemplTestUtility.DeleteTestAsset(testDynamicScaffoldWithEmptyTemplate);
        }

        [Test]
        public void GivenValidScaffold_WhenValidating_ThenShouldNotReturnErrors()
        {
            // Act
            var errors = subject.ValidateScaffoldGeneration(testScaffold, TestTargetPath,
                testInput, testSelection);

            // Verify
            Assert.IsEmpty(errors, "Unexpected errors");
        }

        [Test]
        public void GivenExistingFiles_WhenValidating_ThenShouldReturnOverwriteErrors()
        {
            // Setup
            var existPath = $"{TestTargetPath}/NewDirectory{InputName}/NewFile{testScaffold.name}";
            fileSystemMock.FileExists.Add(existPath);

            // Act
            var errors = subject.ValidateScaffoldGeneration(testScaffold, TestTargetPath,
                testInput, testSelection);

            // Verify
            Assert.IsNotEmpty(errors, "Errors expected");
            Assert.IsTrue(errors[0].Type == TemplScaffoldErrorType.Overwrite, "Wrong error type");
            Assert.IsTrue(errors[0].Message == existPath, "Wrong error message");
        }

        [Test]
        public void GivenEmptyScaffold_WhenValidating_ThenShouldReturnErrors()
        {
            // Setup
            var emptyScaffold = ScriptableObject.CreateInstance<TemplScaffold>();

            // Act
            var errors = subject.ValidateScaffoldGeneration(emptyScaffold, TestTargetPath,
                testInput, testSelection);

            // Verify
            Assert.IsNotEmpty(errors, "Errors expected");
            Assert.IsTrue(errors[0].Type == TemplScaffoldErrorType.Undefined, "Wrong error type");
        }

        [Test]
        public void GivenValidDynamicScaffold_WhenValidating_ThenShouldNotReturnErrors()
        {
            // Act
            var errors = subject.ValidateScaffoldGeneration(testDynamicScaffold, TestTargetPath,
                testInput, testSelection);

            // Verify
            Assert.IsEmpty(errors, "Unexpected errors");
        }

        [Test]
        public void GivenDynamicScaffoldWithNoTemplate_WhenValidating_ThenShouldReturnErrors()
        {
            // Setup
            var emptyScaffold = ScriptableObject.CreateInstance<TemplDynamicScaffold>();

            // Act
            var errors = subject.ValidateScaffoldGeneration(emptyScaffold, TestTargetPath,
                testInput, testSelection);

            // Verify
            Assert.IsNotEmpty(errors, "Errors expected");
            Assert.IsTrue(errors[0].Type == TemplScaffoldErrorType.Template, "Wrong error type");
        }

        [Test]
        public void GivenDynamicScaffoldWithEmptyTemplate_WhenValidating_ThenShouldReturnErrors()
        {
            // Act
            var errors = subject.ValidateScaffoldGeneration(testDynamicScaffoldWithEmptyTemplate,
                TestTargetPath, testInput, testSelection);

            // Verify
            Assert.IsNotEmpty(errors, "Errors expected");
            Assert.IsTrue(errors[0].Type == TemplScaffoldErrorType.Template, "Wrong error type");
        }

        [Test]
        public void GivenDynamicScaffoldWithErrors_WhenValidating_ThenShouldReturnErrors()
        {
            // Setup
            testInput.induce_runtime_error = true;

            // Act
            var errors = subject.ValidateScaffoldGeneration(testDynamicScaffold,
                TestTargetPath, testInput, testSelection);

            // Verify
            Assert.IsNotEmpty(errors, "Errors expected");
            Assert.IsTrue(errors[0].Type == TemplScaffoldErrorType.Template, "Wrong error type");
        }

        private struct InputType
        {
            public string name;
            public string[] elements;
            public bool induce_runtime_error;
        }
    }
}
