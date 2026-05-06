# C&#x266D; (C-Flat) Language Documentation

C-Flat is a statically-typed, imperative programming language inspired by C#. It compiles to .NET CIL (Common Intermediate Language) and produces executable `.exe` assemblies targeting the .NET runtime.

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Program Structure](#program-structure)
3. [Comments](#comments)
4. [Data Types](#data-types)
5. [Variables](#variables)
6. [Operators](#operators)
7. [Print Statement](#print-statement)
8. [Control Flow](#control-flow)
9. [Loops](#loops)
10. [Functions](#functions)
11. [Classes](#classes)
12. [Goto and Labels](#goto-and-labels)
13. [Null](#null)
14. [Grammar Reference](#grammar-reference)

---

## Getting Started

C-Flat programs are written as top-level statements. There is no required `main` function or entry point boilerplate -- you simply write your code at the top level, and the compiler wraps it in an implicit `Main` method.

```csharp
print("Hello, World!");
```

The compiler produces a .NET executable (`.exe`) that can be run on any platform with the .NET runtime installed.

---

## Program Structure

A C-Flat program consists of a sequence of statements executed from top to bottom. Statements include variable declarations, function declarations, class declarations, control flow, and expressions. Statements (except block-level constructs like `if`, `while`, `for`, and class/function declarations) must be terminated with a semicolon (`;`).

```csharp
int x = 5;
print(x);

void Greet()
{
    print("Hello!");
}

Greet();
```

Functions and classes can be declared anywhere in the file and referenced before their declaration (forward references are supported).

---

## Comments

C-Flat supports single-line comments with `//`. Everything after `//` until the end of the line is ignored.

```csharp
// This is a comment
int a = 5; // This is also a comment
```

Multi-line comments are not currently supported.

---

## Data Types

C-Flat has three primitive types and supports user-defined class types:

| Type | Description | Example Literals |
|------|-------------|-----------------|
| `int` | Integer numbers | `42`, `0`, `-5`, `1e10` |
| `string` | Text strings | `"Hello"`, `"He said \"hi\""` |
| `bool` | Boolean values | `true`, `false` |
| *ClassName* | Instance of a user-defined class | `new Cat()` |

Numeric literals support integers, decimals (e.g., `3.14`), and scientific notation (e.g., `1e10`), though the type system treats them as `int`.

String literals support escape sequences with backslash (e.g., `\"` for an embedded quote).

---

## Variables

### Declaration

Declare a variable with its type and name:

```csharp
int a;
string name;
bool isReady;
```

### Declaration with Assignment

```csharp
int a = 5;
string name = "Bob";
bool isReady = true;
```

### Assignment

Assign a new value to a previously declared variable:

```csharp
int a = 1;
a = 2;
```

### Type Safety

C-Flat is statically typed. Assigning a value of the wrong type produces a compile-time error:

```csharp
int a = "Hello";   // ERROR: Expected type 'int' but found string literal.
string b = 42;     // ERROR: Expected type 'string' but found int literal.
bool c = 5;        // ERROR: Expected type 'bool' but found int literal.
```

---

## Operators

### Arithmetic Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `+` | Addition | `3 + 4` |
| `-` | Subtraction | `10 - 3` |
| `*` | Multiplication | `4 * 5` |
| `/` | Division | `10 / 2` |
| `++` | Increment | `a++`, `++a` |
| `--` | Decrement | `a--`, `--a` |

Standard operator precedence applies: `*` and `/` bind tighter than `+` and `-`. Parentheses can override precedence.

```csharp
int a = 3 + 4 * 5;       // a = 23
int b = (3 + 4) * 5;     // b = 35
int c = 3 + 2 * (4 - 1) / 5;  // standard precedence
```

### Increment and Decrement

Both prefix and postfix forms are supported. They can be used as standalone statements or within expressions.

```csharp
int a = 3;
int b = a++;    // b = 3, a = 4 (postfix: returns value then increments)
int c = --b;    // c = 2, b = 2 (prefix: decrements then returns value)
int d = ++a + 2; // a increments to 5, d = 7
a++;            // standalone increment
```

### Comparison Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `=?` | Equal to | `a =? b` |
| `!=` | Not equal to | `a != b` |
| `<` | Less than | `a < 5` |
| `<=` | Less than or equal | `a <= 5` |
| `>` | Greater than | `a > 5` |
| `>=` | Greater than or equal | `a >= 5` |

**Note:** Equality is checked with `=?` (not `==`). This is a distinctive feature of C-Flat.

### Logical Operators

| Operator | Description | Example |
|----------|-------------|---------|
| `&&` | Logical AND | `a > 3 && b < 5` |
| `\|\|` | Logical OR | `a > 3 \|\| b < 5` |
| `!` | Logical NOT | `!done` |

Logical expressions support full precedence: `!` binds tightest, then `&&`, then `||`. Parentheses can group sub-expressions.

```csharp
if(a > 3 && !(b || c) || !d)
{
    print("complex condition");
}
```

### Assignment Operator

| Operator | Description | Example |
|----------|-------------|---------|
| `=` | Assignment | `a = 5` |

---

## Print Statement

The `print` statement outputs a value to the console followed by a newline.

```csharp
print("Hello, World!");   // print a string literal
print(myVariable);        // print the value of a variable
```

`print` accepts string literals, integer variables, string variables, and boolean variables.

---

## Control Flow

### if Statement

```csharp
if(a > 3)
{
    print("Big");
}
```

Curly braces `{}` are **always required** around the body -- there is no braceless single-statement form.

### if / else

```csharp
if(a > 3)
{
    print("Big");
}
else
{
    print("Small");
}
```

### if / else if / else

```csharp
int a = 200;
if(a <= 37)
{
    print("Hi");
}
else if(a < 42)
{
    print("Hey");
}
else
{
    print("Sup");
}
```

### ifn't Statement

C-Flat's unique `ifn't` keyword is the negation of `if` -- the body executes when the condition is **false**.

```csharp
ifn't(a <= 22)
{
    print("Hello");  // prints when a > 22
}
```

`ifn't` supports `else` chains just like `if`:

```csharp
ifn't(a <= 22)
{
    print("Hello");
}
else ifn't(a != 2)
{
    print("Yay");
}
```

You can also mix `if` and `ifn't` in else chains:

```csharp
if(a <= 37)
{
    print("Hi");
}
else ifn't(a <= 22)
{
    print("Hello");
}
else ifn't(a != 2)
{
    print("Yay");
}
```

---

## Loops

### while Loop

```csharp
int x = 1;
while(x < 10)
{
    print(x);
    x = x + 1;
}
```

### while / else

C-Flat supports an `else` clause on `while` loops. The else block executes only if the loop condition was **never** true (the loop body never ran).

```csharp
int x = 5;
while(x < 3)
{
    x = x + 1;
    print("Loop");
}
else
{
    print("Done");  // prints because the loop never executed
}
```

If the loop runs at least once, the else block is skipped:

```csharp
int x = 1;
while(x < 3)
{
    x = x + 1;
    print("Loop");  // prints "Loop" twice
}
else
{
    print("Done");  // does NOT print
}
```

### for Loop

The `for` loop has three parts: initialization, condition, and update.

```csharp
for(int i = 0; i < 14; i++)
{
    print(i);
}
```

The initializer can be a new variable declaration or an assignment to an existing variable:

```csharp
int a = 2;
for(a = 0; a < 14; a = a + 1)
{
    print(a);
}
```

---

## Functions

### Declaration

Functions are declared with a return type, name, parameters, and body:

```csharp
void Greet()
{
    print("Hello!");
}
```

### Parameters

```csharp
void Foo(int a, string b)
{
    for(int i = 0; i < a; i++)
    {
        print(b);
    }
}

Foo(4, "Hello");  // prints "Hello" four times
```

### Return Values

Functions can return values. Use the `return` keyword:

```csharp
int Pow(int a, int b)
{
    int result = 1;
    for(int i = 0; i < b; i++)
    {
        result = result * a;
    }
    return result;
}

int a = Pow(2, 3);  // a = 8
print(a);
```

Void functions can use `return;` with no value to exit early:

```csharp
void Foo()
{
    return;
}
```

### Forward References

Functions can be called before they are declared:

```csharp
Foo(4, "Hello");

void Foo(int a, string b)
{
    for(int i = 0; i < a; i++)
    {
        print(b);
    }
}
```

### Function Overloading

Functions can be overloaded with different parameter signatures:

```csharp
void Greet()
{
    print("Hello!");
}

void Greet(string name)
{
    print(name);
}
```

### Type Checking

The compiler checks that:
- Arguments match parameter types
- The return value matches the declared return type
- Variables assigned from function calls have compatible types

```csharp
string a = Pow(2, 3);  // ERROR: Function 'Pow' returns int, not string.
```

---

## Classes

### Declaration

Classes contain fields and methods:

```csharp
class Cat
{
    string Name;
    int Age;
}
```

### Instantiation

Create instances with the `new` keyword:

```csharp
Cat bob = new Cat();
```

### Field Access

Access fields using dot notation:

```csharp
Cat bob = new Cat();
bob.Name = "Bob";
bob.Age = 3;
print(bob.Name);  // prints "Bob"
print(bob.Age);   // prints "3"
```

### Methods

Classes can contain methods that operate on the instance's fields:

```csharp
class Cat
{
    string Name;
    int Age;

    void SetName(string name)
    {
        Name = name;
    }

    int SetAge(int age)
    {
        Age = age;
        return age;
    }
}

Cat bob = new Cat();
bob.SetName("Bob");
bob.SetAge(2);
print(bob.Name);
print(bob.Age);
```

### Constructors

Constructors have the same name as the class and no explicit return type:

```csharp
class Cat
{
    string Name;
    int Age;

    Cat(string name, int age)
    {
        Name = name;
        Age = age;
    }
}

Cat bob = new Cat("Bob", 2);
print(bob.Name);  // prints "Bob"
```

A default (parameterless) constructor is always available. If you define a parameterless constructor, it replaces the default.

### Constructor Overloading

Classes can have multiple constructors with different parameter lists:

```csharp
class Node
{
    int Value;
    Node Next;

    Node(int value)
    {
        Value = value;
        Next = null;
    }

    Node(int value, Node next)
    {
        Value = value;
        Next = next;
    }
}
```

### Self-Referencing Types

Classes can have fields of their own type, enabling data structures like linked lists:

```csharp
class Node
{
    int Value;
    Node Next;

    Node(int value)
    {
        Value = value;
        Next = null;
    }
}

class LinkedList
{
    Node head;

    LinkedList()
    {
        head = null;
    }

    void AddLast(int value)
    {
        if(head =? null)
        {
            head = new Node(value);
        }
        else
        {
            Node current = head;
            while(current.Next != null)
            {
                current = current.Next;
            }
            current.Next = new Node(value);
        }
    }

    void Print()
    {
        Node current = head;
        while(current != null)
        {
            print(current.Value);
            current = current.Next;
        }
    }
}

LinkedList list = new LinkedList();
list.AddLast(1);
list.AddLast(2);
list.AddLast(3);
list.Print();  // prints 1, 2, 3
```

### Forward References

Classes (like functions) can be used before they are declared:

```csharp
Cat bob = new Cat();
bob.Name = "Bob";

class Cat
{
    string Name;
}
```

### Increment on Class Fields

Increment and decrement operators work on class fields:

```csharp
class Cat
{
    int a;
}

Cat bob = new Cat();
bob.a = 3;
bob.a++;
print(bob.a);  // prints 4
```

---

## Goto and Labels

C-Flat supports `goto` statements for unconditional jumps to labeled positions:

```csharp
int i = 0;
LoopLabel:
print("Loopy");
if(i > 5)
{
    goto EndLoop;
}
i = i + 1;
goto LoopLabel;

EndLoop:
print("Done");
```

Labels are identifiers followed by a colon (`:`). They must be unique within a function scope. `goto` can only jump to labels within the same function.

Note: A label cannot be the last line in a block of code.

---

## Null

The `null` keyword represents the absence of a value. It can be assigned to class-type variables but not to primitive types (`int`, `bool`, `string`).

```csharp
Node next = null;        // valid: class type
```

Null comparisons work with `=?` and `!=`:

```csharp
if(head =? null)
{
    print("empty");
}

while(current != null)
{
    current = current.Next;
}
```

---

## Formal Syntax Definition (BNF)

The following is the complete formal grammar for C-Flat in Extended Backus-Naur Form (EBNF). Non-terminals are in `PascalCase`, terminals are in `"quotes"` or `UPPER_CASE`, and `e` denotes the empty production (epsilon).

### Lexical Grammar

```ebnf
(* Whitespace and Comments *)
WHITESPACE      = { " " | "\t" | "\n" | "\r" } ;
COMMENT         = "//" , { any character except newline } ;

(* Literals *)
NUMERIC         = [ "+" | "-" ] , digit , { digit } , [ "." , digit , { digit } ]
                  , [ ( "e" | "E" ) , [ "+" | "-" ] , digit , { digit } ] ;
STRING          = '"' , { any character except '"' | ( '\' , any character ) } , '"' ;

(* Identifiers and Labels *)
IDENTIFIER      = ( letter | "_" ) , { letter | digit | "_" } ;
LABEL           = IDENTIFIER , ":" ;

(* Letters and Digits *)
letter          = "A" | ... | "Z" | "a" | ... | "z" ;
digit           = "0" | ... | "9" ;
```

### Keywords

```ebnf
keyword         = "print" | "if" | "ifn't" | "else" | "while" | "for"
                | "true" | "false" | "goto" | "return" | "class" | "new" | "null" ;
```

### Phrase Grammar

```ebnf
(* === Program Structure === *)

Program             = PossibleStatements , Program
                    | e ;

PossibleStatements  = ClassDeclaration
                    | PrintStatement , ";"
                    | VariableExpr , ";"
                    | IfStatement
                    | IfntStatement
                    | WhileLoop
                    | ForLoop
                    | LABEL
                    | GotoStatement , ";"
                    | FunctionDeclaration
                    | FunctionCall , ";"
                    | ReturnStatement , ";" ;


(* === Print === *)

PrintStatement      = "print" , "(" , STRING , ")"
                    | "print" , "(" , VariableName , ")" ;


(* === Variables === *)

VariableExpr        = VariableDeclarationAndAssignment
                    | VariableDeclaration
                    | VariableAssignment ;

VariableDeclaration = IDENTIFIER , IDENTIFIER ;
                    (* type name *)

VariableDeclarationAndAssignment
                    = IDENTIFIER , IDENTIFIER , "=" , VariableValue ;
                    (* type name = value *)

VariableAssignment  = VariableName , "=" , VariableValue
                    | Incrementer ;

VariableValue       = "null"
                    | MathExpr
                    | STRING
                    | BoolExpr
                    | ClassInstantiation ;

VariableName        = IDENTIFIER , "." , VariableName
                    | IDENTIFIER ;

Incrementer         = VariableName , "++"
                    | VariableName , "--"
                    | "++" , VariableName
                    | "--" , VariableName ;


(* === Math Expressions === *)
(* Implements standard operator precedence via recursive descent *)

MathExpr            = MathTerm , MathExprTail ;

MathExprTail        = "+" , MathTerm , MathExprTail
                    | "-" , MathTerm , MathExprTail
                    | e ;

MathTerm            = MathFactor , MathTermTail ;

MathTermTail        = "*" , MathFactor , MathTermTail
                    | "/" , MathFactor , MathTermTail
                    | e ;

MathFactor          = "(" , MathExpr , ")"
                    | NUMERIC
                    | ExpressionIncrementer
                    | FunctionCall
                    | VariableName
                    | "null" ;

ExpressionIncrementer
                    = VariableName , "++"
                    | VariableName , "--"
                    | "++" , VariableName
                    | "--" , VariableName ;


(* === Boolean Expressions === *)
(* Precedence: NOT > AND > OR *)

BoolExpr            = BoolAndExpr , BoolOrExprTail ;

BoolOrExprTail      = "||" , BoolAndExpr , BoolOrExprTail
                    | e ;

BoolAndExpr         = BoolFactor , BoolAndExprTail ;

BoolAndExprTail     = "&&" , BoolFactor , BoolAndExprTail
                    | e ;

BoolFactor          = "!" , BoolFactor
                    | "(" , BoolExpr , ")"
                    | Comparison
                    | BoolLiteral
                    | VariableName ;

Comparison          = MathExpr , BoolRelativeOp , MathExpr ;

BoolRelativeOp      = "<" | "<=" | ">" | ">=" | "=?" | "!=" ;

BoolLiteral         = "true" | "false" ;


(* === Control Flow === *)

IfStatement         = "if" , "(" , BoolExpr , ")" , "{" , Program , "}" , IfFollowUp ;

IfntStatement       = "ifn't" , "(" , BoolExpr , ")" , "{" , Program , "}" , IfFollowUp ;

IfFollowUp          = "else" , "{" , Program , "}"
                    | "else" , IfStatement
                    | "else" , IfntStatement
                    | e ;

WhileLoop           = "while" , "(" , BoolExpr , ")" , "{" , Program , "}" , WhileFollowUp ;

WhileFollowUp       = "else" , "{" , Program , "}"
                    | e ;

ForLoop             = "for" , "(" , VariableDeclarationAndAssignment , ";" , BoolExpr , ";" ,
                      VariableAssignment , ")" , "{" , Program , "}"
                    | "for" , "(" , VariableAssignment , ";" , BoolExpr , ";" ,
                      VariableAssignment , ")" , "{" , Program , "}" ;

GotoStatement       = "goto" , IDENTIFIER ;


(* === Functions === *)

FunctionDeclaration = IDENTIFIER , IDENTIFIER , "(" , ")" , "{" , Program , "}"
                    | IDENTIFIER , IDENTIFIER , "(" , FunctionParameter , ")" ,
                      "{" , Program , "}" ;
                    (* returnType name ( params ) { body } *)

FunctionCall        = IDENTIFIER , "(" , ")"
                    | IDENTIFIER , "(" , FunctionCallParameter , ")"
                    | IDENTIFIER , "." , IDENTIFIER , "(" , ")"
                    | IDENTIFIER , "." , IDENTIFIER , "(" , FunctionCallParameter , ")" ;

FunctionParameter   = IDENTIFIER , IDENTIFIER , "," , FunctionParameter
                    | IDENTIFIER , IDENTIFIER ;
                    (* type name [, more] *)

FunctionCallParameter
                    = VariableValue , "," , FunctionCallParameter
                    | VariableValue ;

ReturnStatement     = "return" , VariableValue
                    | "return" ;


(* === Classes === *)

ClassDeclaration    = "class" , IDENTIFIER , "{" , ClassBody , "}" ;

ClassBody           = ClassMember , ClassBody
                    | e ;

ClassMember         = FunctionDeclaration
                    | ConstructorDeclaration
                    | VariableDeclaration , ";" ;

ConstructorDeclaration
                    = IDENTIFIER , "(" , ")" , "{" , Program , "}"
                    | IDENTIFIER , "(" , FunctionParameter , ")" , "{" , Program , "}" ;
                    (* ClassName ( params ) { body } *)

ClassInstantiation  = "new" , IDENTIFIER , "(" , ")"
                    | "new" , IDENTIFIER , "(" , FunctionCallParameter , ")" ;
```

### Grammar Notes

1. **Ambiguity Resolution**: The parser tries productions in the order listed. The first successful match wins. For example, `VariableExpr` tries `VariableDeclarationAndAssignment` before `VariableDeclaration` to ensure the `= value` part is consumed when present.

2. **Left-Recursion Elimination**: Math and boolean expressions use tail-recursive productions (`MathExprTail`, `BoolOrExprTail`, etc.) to avoid left-recursion while preserving left-associativity.

3. **Epsilon Productions**: Productions that can match nothing (marked `e`) allow optional parts of the grammar, such as `IfFollowUp` (an `if` may or may not have an `else`).

4. **Operator Precedence Encoding**: Arithmetic precedence is structurally encoded: `MathExpr` handles `+`/`-`, `MathTerm` handles `*`/`/`, and `MathFactor` handles atoms and parenthesized sub-expressions. Similarly, `BoolExpr` > `BoolAndExpr` > `BoolFactor` encodes `||` < `&&` < `!`.

5. **VariableName vs IDENTIFIER**: `VariableName` allows dot-separated access (e.g., `bob.Name`), while bare `IDENTIFIER` is used where only a simple name is valid (e.g., type names, function names).

---

## Grammar Reference

### Identifiers

Identifiers start with a letter or underscore, followed by letters, digits, or underscores:
```
[A-Za-z_][A-Za-z0-9_]*
```

### Reserved Keywords

```
print  if  ifn't  else  while  for  true  false
goto  return  class  new  null
```

### Operator Precedence (highest to lowest)

| Precedence | Operators | Associativity |
|-----------|-----------|---------------|
| 1 | `!` `++` `--` (prefix) | Right |
| 2 | `*` `/` | Left |
| 3 | `+` `-` | Left |
| 4 | `<` `<=` `>` `>=` `=?` `!=` | Left |
| 5 | `&&` | Left |
| 6 | `\|\|` | Left |
| 7 | `=` | Right |

### Statement Types

| Statement | Syntax |
|-----------|--------|
| Variable declaration | `type name;` |
| Variable declaration + assignment | `type name = value;` |
| Variable assignment | `name = value;` |
| Print | `print(expr);` |
| If | `if(condition) { body }` |
| Ifn't | `ifn't(condition) { body }` |
| While | `while(condition) { body }` |
| For | `for(init; condition; update) { body }` |
| Function declaration | `type name(params) { body }` |
| Function call | `name(args);` |
| Method call | `obj.method(args);` |
| Return | `return value;` or `return;` |
| Goto | `goto label;` |
| Label | `name:` |
| Class declaration | `class Name { members }` |
| Instantiation | `new ClassName(args)` |
| Increment/Decrement | `name++;` `--name;` |

---

## Scoping Rules

- Variables declared inside `if`, `while`, `for`, or function bodies are local to that block.
- Variables declared in an outer scope are accessible in inner scopes.
- Accessing a variable declared in an inner scope from an outer scope is a compile-time error.
- Class fields are accessible from any method within the class.
- Function parameters are scoped to the function body.

```csharp
int a = 2;
if(a > 1)
{
    int e = 42;  // e is local to this block
    a = 14;      // a is accessible from outer scope
}
e = 3;           // ERROR: Variable 'e' not declared in scope.
```

---

## Complete Example: Binary Search Guessing Game

```csharp
int answer = 37;

int min = 0;
int max = 100;
bool notDone = true;
while(notDone)
{
    int guess = (min + max) / 2;
    print(guess);
    if(guess < answer)
    {
        print("Too low");
        min = guess;
    }
    if(guess > answer)
    {
        print("Too high");
        max = guess;
    }
    if(guess =? answer)
    {
        print("Correct!");
        notDone = false;
    }
}
print("Goodbye");
```

Output:
```
50
Too high
25
Too low
37
Correct!
Goodbye
```

---

## Differences from C#

| Feature | C# | C-Flat |
|---------|------|--------|
| Equality check | `==` | `=?` |
| Negated if | `if(!cond)` | `ifn't(cond)` |
| While-else | Not supported | `while(cond) {} else {}` |
| Access modifiers | `public`, `private`, etc. | Not yet implemented |
| Inheritance | Supported | Not yet implemented |
| Arrays | Supported | Not yet implemented |
| String interpolation | `$"Hello {name}"` | Not yet implemented |
| Braces optional | For single statements | Always required |

---

## Compilation Pipeline

The C-Flat compiler operates in four phases:

1. **Lexer/Tokenizer** -- Converts source text into a stream of tokens (keywords, operators, identifiers, literals, etc.)
2. **Parser** -- Builds a Concrete Syntax Tree (CST) from tokens, then transforms it into an Abstract Syntax Tree (AST)
3. **Semantic Analyzer** -- Performs type checking, scope validation, and function/class resolution
4. **Code Generator** -- Emits .NET CIL bytecode, producing a runnable `.exe` assembly
