using Compiler;
using Compiler.Tokens;

using Mono.Cecil;

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text;


//Excellent — now we’re truly debugging the cake. 🍰

namespace TestProject
{
    [TestClass]
    public sealed class Parsing
    {
        [TestMethod]
        public void PrintTest()
        {
            bool success = Lexer.Lex("print(\"Hello\");", out var result);
            Assert.IsTrue(success);

            var messages = Parser.Parse(result, out var tree);
            ;
            Assert.IsTrue(messages.Count == 0, string.Join("\n", messages));
        }

        [TestMethod]
        public void MathTest()
        {
            bool success = Lexer.Lex("int a = 3 + 4;\n" +
                                     "b = 3 + 4 * 5;\n" +
                                     "int c = (1 + 2) * 3;\n" +
                                     "int d = 4 * (7 - 2);\n" +
                                     "int e = 42;\n" +
                                     "int f = 3 + 2 * (4 - 1) / 5;\n" +
                                     "int g = 5 - 3 - 2;", out var result);
            Assert.IsTrue(success);


            ParseNode? twee;

            var messages = Parser.Parse(result, out twee);
            ;
        }

        [TestMethod]
        public void IfTest()
        {
            bool success = Lexer.Lex("if(a > 3 && !(b || c) || d)\n" +
                                     "{\n" +
                                     "int e = 42;\n" +
                                     "}\n", out var result);
            Assert.IsTrue(success);

            ParseNode? twee;

            var messages = Parser.Parse(result, out twee);
            ;
        }

        [TestMethod]
        public void ElseTest()
        {
            bool success = Lexer.Lex("int a = 200;\n" +
                                     "if (a <= 37)\n" +
                                     "{\n" +
                                     "    print(\"Hi\");\n" +
                                     "}\n" +
                                     "else if (a < 42)\n" +
                                     "{\n" +
                                     "    print(\"Hey\");\n" +
                                     "}\n" +
                                     "else\n" +
                                     "{\n" +
                                     "    print(\"Sup\");\n" +
                                     "}\n" +
                                     "\n" +
                                     "ifn't(a <= 22)\n" +
                                     "{\n" +
                                     "    print(\"Hello\");\n" +
                                     "}\n" +
                                     "else ifn't(a != 2)\n" +
                                     "{\n" +
                                     "    print(\"Yay\");\n" +
                                     "}", out var result);
            Assert.IsTrue(success);

            ParseNode? twee;

            var messages = Parser.Parse(result, out twee);
            ;
        }

        [TestMethod]
        public void ScopeTest()
        {
            bool success;
            List<IToken> result;

            ParseNode? twee;

            List<string> messages;
            //#############################


            success = Lexer.Lex("int a = 2;\n" +
                                "if(a > 3 && !(b || c) || !d)\n" +
                                "{\n" +
                                "int e = 42;\n" +
                                "int b;\n" +
                                "a = 14;\n" +
                                "}\n" +
                                "e = 3;", out result);

            messages = Parser.Parse(result, out twee);
        }
    }

    [TestClass]
    [DoNotParallelize]
    public sealed class Semantic
    {
        public static void ValidTypeTest(string program)
        {
            List<string> messages;

            Assert.IsTrue(Lexer.Lex(program, out List<IToken> result));

            messages = Parser.Parse(result, out ParseNode? twee);

            if (messages.Count > 0) throw new Exception("Did not parse.");

            messages = SemanticAnalyzer.Analyze(twee!, out var scopes);

            if (messages.Count > 0) throw new Exception("Did not pass analysis.");
        }
        public static void TypeMismatchTest(string program)
        {
            List<string> messages;

            Assert.IsTrue(Lexer.Lex(program, out List<IToken> result), "Did not tokenize.");

            messages = Parser.Parse(result, out ParseNode? twee);

            Assert.IsFalse(messages.Count > 0, "Did not parse.");

            messages = SemanticAnalyzer.Analyze(twee!, out var scopes);

            Assert.IsTrue(messages.Count > 0, "Type mismatch missed.");
        }

        #region Successful type checks
        [TestMethod, TestCategory("Successful type check")]
        public void StandardTypeTest()
        {
            string program =
                "int a = 2;\n" +
                "string b = \"Hi\";\n" +
                "bool c = true;\n" +

                "a = 1;\n" +
                "b = \"Bye\";\n" +
                "c = false;\n" +

                "print(a);\n" +
                "print(b);\n" +
                "print(c);\n";
            ValidTypeTest(program);
        }
        [TestMethod, TestCategory("Successful type check")]
        public void FunctionParamTypeTest()
        {
            string program =
                "Foo(4, \"Hello\");\r\n" +
                "void Foo(int a, string b)\r\n" +
                "{\r\n" +
                    "\tfor(string i = 0; i < a; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(b);\r\n" +
                    "\t}\r\n" +
                "}\r\n";
            ValidTypeTest(program);
        }
        [TestMethod, TestCategory("Successful type check")]
        public void FunctionReturnTypeTest()
        {
            string program =
                "int a = Pow(2, 3);\r\n" +
                "print(a);" +
                "int Pow(int a, int b)\r\n" +
                "{\r\n" +
                    "\tint result = 1;\r\n" +
                    "\tfor(int i = 0; i < b; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tresult = result * a;\r\n" +
                    "\t}\r\n" +
                    "\treturn result;\r\n" +
                "}\r\n" +
                "\r\n";
            ValidTypeTest(program);
        }

        [TestMethod, TestCategory("Successful type check"), TestCategory("Classes")]
        public void BasicClassTest()
        {
            string program =
                "Cat bob = new Cat();\r\n" +
                "bob.Name = \"Bob\";\r\n" +
                "print(bob.Name);\r\n" +
                "print(bob.Age);\r\n" +
                "\r\n" +
                "class Cat\r\n" +
                "{\r\n" +
                "    string Name;\r\n" +
                "    int Age;\r\n" +
                "    \r\n" +
                "    void SetName(string name)\r\n" +
                "    {\r\n" +
                "        Name = name;\r\n" +
                "    }\r\n" +
                "    int SetAge(int age)\r\n" +
                "    {\r\n" +
                "        Age = age;\r\n" +
                "        return age;\r\n" +
                "    }\r\n" +
                "}";
            TypeMismatchTest(program);
        }
        #endregion


        #region Basic type mismatch tests
        /*
        "int a = 2;\n" +
        "string b = \"Hi\";\n" +
        "bool c = true;\n" +

        "a = 1;\n" +
        "a = \"Bye\";\n" +
        "a = false;\n" +
        "a = b;\n" +
        "a = c;\n" +

        "b = 2;\n" +
        "b = \"Bye\";\n" +
        "b = true;\n" +
        "b = a;\n" +
        "b = c;\n" +

        "c = 14;\n" +
        "c = \"Hello\";\n" +
        "c = true;\n" +
        "c = a;\n" +
        "c = b;\n" +

        "if(a > b && c || b){}\n" +

        "print(a);\n" +
        "print(b);\n" +
        "print(c);\n" +
        "print(\"Hi\");\n"
        */
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitIntWithBool()
        {
            string program = "int a = true;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitIntWithString()
        {
            string program = "int a = \"Hi\";\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetIntToString()
        {
            string program =
                "int a = 2;\n" +
                "a = \"Bye\";\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetIntToBool()
        {
            string program =
                "int a = 2;\n" +
                "a = false;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetIntToStringVar()
        {
            string program =
                "int a = 2;\n" +
                "string b = \"Hi\";\n" +
                "a = b;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetIntToBoolVar()
        {
            string program =
                "int a = 2;\n" +
                "bool c = true;\n" +
                "a = c;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitStringWithInt()
        {
            string program = "string b = 3;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitStringWithBool()
        {
            string program = "string b = true;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetStringToInt()
        {
            string program =
                "string b = \"Hi\";\n" +
                "b = 2;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetStringToBool()
        {
            string program =
                "string b = \"Hi\";\n" +
                "b = false;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetStringToIntVar()
        {
            string program =
                "int a = 2;\n" +
                "string b = \"Hi\";\n" +
                "b = a;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetStringToBoolVar()
        {
            string program =
                "string b = \"Hi\";\n" +
                "bool c = true;\n" +
                "b = c;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitBoolWithInt()
        {
            string program = "bool c = 3;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void InitBoolWithString()
        {
            string program = "bool c = \"Hi\";\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetBoolToInt()
        {
            string program =
                "bool c = true;\n" +
                "c = 2;\n";
            TypeMismatchTest(program);

        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetBoolToString()
        {
            string program =
                "bool c = true;\n" +
                "c = \"Hi\";\n";
            TypeMismatchTest(program);

        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetBoolToIntVar()
        {
            string program =
                "int a = 2;\n" +
                "bool c = true;\n" +
                "c = a;\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Standard type mismatch")]
        public void SetBoolToStringVar()
        {
            string program =
                "string b = \"Hi\";\n" +
                "bool c = true;\n" +
                "c = b;\n";
            TypeMismatchTest(program);
        }
        #endregion

        #region Function type mismatch
        [TestMethod, TestCategory("Function type mismatch")]
        public void FunctionParamTypeMismatchTest()
        {
            string program =
                "Foo(4, true);\r\n" +
                "void Foo(int a, string b)\r\n" +
                "{\r\n" +
                    "\tfor(string i = 0; i < a; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(b);\r\n" +
                    "\t}\r\n" +
                "}\r\n";
            TypeMismatchTest(program);
        }
        [TestMethod, TestCategory("Function type mismatch")]
        public void FunctionReturnTypeMismatchTest()
        {
            string program =
                "string a = Pow(2, 3);\r\n" +
                "print(a);" +
                "int Pow(int a, int b)\r\n" +
                "{\r\n" +
                    "\tint result = 1;\r\n" +
                    "\tfor(int i = 0; i < b; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tresult = result * a;\r\n" +
                    "\t}\r\n" +
                    "\treturn result;\r\n" +
                "}\r\n" +
                "\r\n";
            TypeMismatchTest(program);
        }

        [TestMethod, TestCategory("Function type mismatch"), TestCategory("Classes")]
        public void FunctionReturnTypeInsideClassMismatchTest()
        {
            string program =
                "Cat bob = new Cat();\r\n" +
                "bob.Name = \"Bob\";\r\n" +
                "string a = bob.SetAge(2);\r\n" +
                "\r\n" +
                "class Cat\r\n" +
                "{\r\n" +
                "    string Name;\r\n" +
                "    int Age;\r\n" +
                "    \r\n" +
                "    void SetName(string name)\r\n" +
                "    {\r\n" +
                "        Name = name;\r\n" +
                "    }\r\n" +
                "    int SetAge(int age)\r\n" +
                "    {\r\n" +
                "        Age = age;\r\n" +
                "        return age;\r\n" +
                "    }\r\n" +
                "}";
            TypeMismatchTest(program);
        }
        #endregion
    }

    [TestClass]
    public sealed class FullProgram
    {
        #region Helpers
        private static string LogTree(ParseNode node, int depth = 0)
        {
            StringBuilder sb = new();
            sb.AppendLine(new string(' ', depth * 2) + (node is ASTNode ? node : node.GetType().Name));
            foreach (var child in node.Children)
            {
                if (child is ParseNode childNode)
                {
                    sb.AppendLine(LogTree(childNode, depth + 1));
                }
            }

            return sb.ToString();
        }

        private static void CheckEntryPoint(string file)
        {
            using var pe = new PEReader(File.OpenRead(file));

            var cor = pe.PEHeaders.CorHeader;
            Assert.IsNotNull(cor);

            var entry = cor.EntryPointTokenOrRelativeVirtualAddress;

            // 0 means "no entry point"
            Assert.AreNotEqual(0, entry);
        }
        private static void CheckMethodExists(string file, string methodName)
        {
            using var pe = new PEReader(File.OpenRead(file));
            Assert.IsTrue(pe.HasMetadata);
            var md = pe.GetMetadataReader();
            bool found = false;
            foreach (var handle in md.MethodDefinitions)
            {
                var method = md.GetMethodDefinition(handle);
                var name = md.GetString(method.Name);
                if (name == methodName)
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, $"Method '{methodName}' not found.");
        }

        private static void CheckOutput(string file, string expected)
        {
            var asm = Assembly.Load(File.ReadAllBytes(file));
            var entry = asm.EntryPoint;

            // Force JIT
            RuntimeHelpers.PrepareMethod(entry!.MethodHandle);

            // Capture output
            using var sw = new StringWriter();
            Console.SetOut(sw);

            entry.Invoke(null, null);

            var output = sw.ToString();

            Assert.AreEqual(expected, output);
        }

        private static void Compile(string source, string assemblyName)
        {
            List<string> messages;

            bool success = Lexer.Lex(source, out List<IToken> result);

            messages = Parser.Parse(result, out ParseNode? twee);

            if (messages.Count > 0) throw new Exception("Did not parse.");

            messages = SemanticAnalyzer.Analyze(twee!, out var scopes);

            if (messages.Count > 0) throw new Exception("Did not pass analysis.");

            //CodeGen.GenerateCode(twee!, scopes, assemblyName);
        }
        #endregion

        [TestMethod]
        [DoNotParallelize]
        public void WhileElseTest()
        {
            string program =
                "int x = 5;\n" +
                "while(x < 3)\n" +
                "{\n" +
                "    x = x + 1;\n" +
                "    print(\"Loop\");" +
                "}\n" +
                "else\n" +
                "{\n" +
                "    print(\"Done\");\n" +
                "}\n";

            string expectedOutput = "Done\r\n";

            string name = "WhileElse";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);


            program =
                "int x = 1;\n" +
                "while(x < 3)\n" +
                "{\n" +
                "    x = x + 1;\n" +
                "    print(\"Loop\");" +
                "}\n" +
                "else\n" +
                "{\n" +
                "    print(\"Done\");\n" +
                "}\n";

            expectedOutput = "Loop\r\nLoop\r\n";

            name = "WhileElse";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        [DoNotParallelize]
        public void FunctionTest()
        {
            string program =
                "void Foo()\r\n" +
                "{\r\n" +
                    "\tprint(\"Bar\");\r\n" +
                "}\r\n" +
                "\r\n" +
                "int x = 5;\r\n" +
                "Foo();";

            string expectedOutput = "Bar\r\n";

            string name = "Function";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckMethodExists($"{name}.exe", "Foo");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        [DoNotParallelize]
        public void ForLoopTest()
        {
            string program =
                "for(int i = 0; i < 14; i++)\r\n" +
                "{\r\n" +
                "\tprint(i);\r\n" +
                "\tprint(\"Hello\");\r\n" +
                "}\r\n" +
                "\r\n" +
                "int a = 2;\r\n" +
                "for(a = 0; a < 14; a = a + 1)\r\n" +
                "{\r\n" +
                "\tprint(a);\r\n" +
                "}";

            string expectedOutput =
                "0\r\n" +
                "Hello\r\n" +
                "1\r\n" +
                "Hello\r\n" +
                "2\r\n" +
                "Hello\r\n" +
                "3\r\n" +
                "Hello\r\n" +
                "4\r\n" +
                "Hello\r\n" +
                "5\r\n" +
                "Hello\r\n" +
                "6\r\n" +
                "Hello\r\n" +
                "7\r\n" +
                "Hello\r\n" +
                "8\r\n" +
                "Hello\r\n" +
                "9\r\n" +
                "Hello\r\n" +
                "10\r\n" +
                "Hello\r\n" +
                "11\r\n" +
                "Hello\r\n" +
                "12\r\n" +
                "Hello\r\n" +
                "13\r\n" +
                "Hello\r\n" +
                "0\r\n" +
                "1\r\n" +
                "2\r\n" +
                "3\r\n" +
                "4\r\n" +
                "5\r\n" +
                "6\r\n" +
                "7\r\n" +
                "8\r\n" +
                "9\r\n" +
                "10\r\n" +
                "11\r\n" +
                "12\r\n" +
                "13\r\n";

            string name = "ForLoop";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        [DoNotParallelize]
        public void GotoTest()
        {
            string program =
                "int i = 0;\r\n" +
                "LoopLabel:\r\n" +
                "print(\"Loopy\");\r\n" +
                "if(i > 5)\r\n" +
                "{\r\n" +
                    "\tgoto EndLoop;\r\n" +
                "}\r\n" +
                "i = i + 1;\r\n" +
                "goto LoopLabel;\r\n" +
                "\r\n" +
                "EndLoop:\r\n" +
                "print(\"Done\");";

            string expectedOutput =
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Loopy\r\n" +
                "Done\r\n";

            string name = "Goto";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        [DoNotParallelize]
        public void IfElseTest()
        {
            string program =
                "int a = 200;\r\n" +
                "\r\n" +
                "if(a <= 37)\r\n" +
                "{\r\n" +
                    "\tprint(\"Hi\");\r\n" +
                "}\r\n" +
                "else if(a < 37)\r\n" +
                "{\r\n" +
                    "\tprint(\"Hey\");\r\n" +
                "}\r\n" +
                "else\r\n" +
                "{\r\n" +
                    "\tprint(\"Sup\");\r\n" +
                "}\r\n" +
                "\r\n" +
                "ifn't(a <= 37)\r\n" +
                "{\r\n" +
                    "\tprint(\"Hello\");\r\n" +
                "}\r\n" +
                "else ifn't(a != 2)\r\n" +
                "{\r\n" +
                    "\tprint(\"Yay\");\r\n" +
                    "\tprint(a);\r\n" +
                "}";

            string expectedOutput =
                "Sup\r\n" +
                "Hello\r\n";

            string name = "IfElse";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        [DoNotParallelize]
        public void GuessingGameTest()
        {
            string program =
                "int answer = 37;\r\n" +
                "\r\n" +
                "int min = 0;\r\n" +
                "int max = 100;\r\n" +
                "bool notDone = true;\r\n" +
                "while(notDone)\r\n" +
                "{\r\n" +
                    "\tint guess = (min + max) / 2;\r\n" +
                    "\tprint(guess);\r\n" +
                    "\tif(guess < answer)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(\"Too low\");\r\n" +
                        "\t\tmin = guess;\r\n" +
                    "\t}\r\n" +
                    "\tif(guess > answer)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(\"Too high\");\r\n" +
                        "\t\tmax = guess;\r\n" +
                    "\t}\r\n" +
                    "\tif(guess =? answer)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(\"Correct!\");\r\n" +
                        "\t\tnotDone = false;\r\n" +
                    "\t}\r\n" +
                    "\tint a = 4;\r\n" +
                "}\r\nprint(\"Goodbye\");";

            string expectedOutput =
                "50\r\n" +
                "Too high\r\n" +
                "25\r\n" +
                "Too low\r\n" +
                "37\r\n" +
                "Correct!\r\n" +
                "Goodbye\r\n";

            string name = "GuessingGame";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        [DoNotParallelize]
        public void IncrementerTest()
        {
            string program =
                "int a = 3;\r\n" +
                "int b = a++;\r\n" +
                "int c = --b;\r\n" +
                "int d = ++a + 2;\r\n" +
                "int e = d;\r\n" +
                "a++;\r\n" +
                "print(a);\r\n" +
                "print(b);\r\n" +
                "print(c);\r\n" +
                "print(d);\r\n" +
                "print(e);\r\n";

            string expectedOutput =
                "6\r\n" +
                "2\r\n" +
                "2\r\n" +
                "7\r\n" +
                "7\r\n";

            string name = "Incrementer";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        [DoNotParallelize]
        public void FunctionParamTest()
        {
            string program =
                "void Foo(int a, string b)\r\n" +
                "{\r\n" +
                    "\tfor(string i = 0; i < a; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tprint(b);\r\n" +
                    "\t}\r\n" +
                "}\r\n" +
                "\r\n" +
                "Foo(4, \"Hello\");\r\n";

            string expectedOutput =
                "Hello\r\n" +
                "Hello\r\n" +
                "Hello\r\n" +
                "Hello\r\n";

            string name = "FunctionParam";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckMethodExists($"{name}.exe", "Foo");
            CheckOutput($"{name}.exe", expectedOutput);
        }


        [TestMethod]
        [DoNotParallelize]
        public void FunctionReturnTest()
        {
            string program =
                "int Pow(int a, int b)\r\n" +
                "{\r\n" +
                    "\tint result = 1;\r\n" +
                    "\tfor(int i = 0; i < b; i++)\r\n" +
                    "\t{\r\n" +
                        "\t\tresult = result * a;\r\n" +
                    "\t}\r\n" +
                    "\treturn result;\r\n" +
                "}\r\n" +
                "\r\n" +
                //"print(Pow(2, 3));\r\n";
                "int a = Pow(2, 3);\r\n" +
                "print(a);";

            string expectedOutput =
                "8\r\n";

            string name = "FunctionReturn";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckMethodExists($"{name}.exe", "Pow");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        [DoNotParallelize]
        public void ClassTest()
        {
            string program =
                "Cat bob = new Cat();\r\n" +
                "bob.Name = \"Bob\";\r\n" +
                "print(bob.Name);\r\n" +
                "print(bob.Age);\r\n" +
                "\r\n" +
                "class Cat\r\n" +
                "{\r\n" +
                "    string Name;\r\n" +
                "    int Age;\r\n" +
                "    \r\n" +
                "    void SetName(string name)\r\n" +
                "    {\r\n" +
                "        Name = name;\r\n" +
                "    }\r\n" +
                "    int SetAge(int age)\r\n" +
                "    {\r\n" +
                "        Age = age;\r\n" +
                "        return age;\r\n" +
                "    }\r\n" +
                "}";

            string expectedOutput =
                "Bob\r\n";

            string name = "Constructor";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }

        [TestMethod]
        [DoNotParallelize]
        public void ConstructorTest()
        {
            string program =
                "Cat bob = new Cat(\"Bob\", 2);\r\n" +
                "bob.Age = 3;\r\n" +
                "print(bob.Name);\r\n" +
                "\r\n" +
                "class Cat\r\n" +
                "{\r\n" +
                    "\tstring Name;\r\n" +
                    "\tint Age;\r\n" +
                    "\t\r\n" +
                    "\tCat(string name, int age)\r\n" +
                    "\t{\r\n" +
                        "\t\tName = name;\r\n" +
                        "\t\tAge = age;\r\n" +
                    "\t}\r\n" +
                "}";

            string expectedOutput =
                "Bob\r\n";

            string name = "Constructor";

            Compile(program, name);

            CheckEntryPoint($"{name}.exe");
            CheckOutput($"{name}.exe", expectedOutput);
        }
    }
}