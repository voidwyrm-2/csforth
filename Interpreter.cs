namespace CSForth;

using static Common;

class ForthException(string message) : Exception(message) { }

public readonly struct Context(Ref<int> i, string[] tokens, int? loopIndex, Dictionary<string, int> variables, ForthMemory memory)
{
    public readonly Ref<int> i = i;
    public readonly string[] tokens = tokens;
    public readonly int? loopIndex = loopIndex;
    public readonly Dictionary<string, int> variables = variables;
    public readonly ForthMemory memory = memory;
}

public readonly struct Word
{
    public readonly string[] words;
    public readonly Func<Context, Dictionary<string, Word>, Interpreter, ForthStack, string?>? builtin;
    public readonly string effect;

    public Word(string[] words, string effect = ForthStack.IOTypes.Nop)
    {
        this.words = words;
        builtin = null;
        this.effect = effect;
    }

    public Word(Func<Context, Dictionary<string, Word>, Interpreter, ForthStack, string?> builtin, string effect = ForthStack.IOTypes.Nop)
    {
        words = [];
        this.builtin = builtin;
        this.effect = effect;
    }
}

public class ForthStack : Stack<int>
{
    public static class IOTypes
    {
        public const string Nop = "--";
        public const string InputOnly = "n --";
        public const string OutputOnly = "-- n";
        public const string InputOutput = "n -- n";
    }

    public const int True = -1;
    public const int False = 0;

    public bool Empty => Count == 0;

    public new void Push(int i) => base.Push(i);

    public new int Pop()
    {
        if (Empty) throw new ForthException("STACK UNDERFLOW");
        return base.Pop();
    }

    public void Dup()
    {
        int i = Pop();
        Push(i);
        Push(i);
    }

    public void Drop() => Pop();
}

public class ForthMemory(uint size)
{
    private readonly int[] bytes = new int[size];

    public uint Size => (uint)bytes.Length;

    private void CheckIndex(int i)
    {
        if (i < 0 || i >= bytes.Length) throw new ForthException($"INVALID MEMORY POSITION {i}");
    }

    public int Get(int i)
    {
        CheckIndex(i);
        return bytes[i];
    }

    public void Set(int i, byte b)
    {
        CheckIndex(i);
        bytes[i] = b;
    }
}

public class Interpreter(ForthStack? stack = null, Dictionary<string, Word>? words = null, uint memorySize = 128)
{
    public readonly ForthStack stack = stack ?? [];

    public readonly Dictionary<string, Word> words = words ?? Stdlib.words;

    public readonly ForthMemory memory = new(memorySize);

    public bool MMode => memory.Size == 0;

    internal void CheckMMode()
    {
        if (!MMode) throw new ForthException("NOT IN MEMORY MODE");
    }

    public void Interpret(string[] tokens, Dictionary<string, int>? injectedVariables = null, int? loopIndex = null)
    {
        //Console.WriteLine(words.Count);
        //foreach (var (k, v) in words) Console.WriteLine(k, v);
        //foreach (var token in tokens) Console.WriteLine(token + "\n");

        Dictionary<string, int> variables = injectedVariables ?? [];

        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].TryOutInt(out int integer))
                stack.Push(integer);
            else if (words.TryGetValue(tokens[i], out Word word))
            {
                if (word.builtin != null)
                {
                    var r = new Ref<int>(i);
                    try
                    {
                        word.builtin(new(r, tokens, loopIndex, variables, memory), words, this, stack);
                    }
                    catch (ForthException e)
                    {
                        throw new ForthException($"{e.Message}\n{tokens.GetSurroundingContext(r)}");
                    }
                    if (r.Set) i = r.Value == -1 ? tokens.Length : r.Value;
                }
                else
                    Interpret(word.words, loopIndex: loopIndex);
            }
            else
                throw new ForthException($"UNKNOWN WORD\n{tokens.GetSurroundingContext(i)}");
        }
    }

    public void Interpret(string text)
    {
        var tokens = text.Split([' ', '\t', '\n']);
        if (tokens.Contains(""))
        {
            List<string> tokensClean = [];
            foreach (var token in tokens)
                if (token != "") tokensClean.Add(token);

            Interpret([.. tokensClean]);
        }
        else
            Interpret(tokens);
    }

    public Interpreter Copy(ForthStack? stack = null, Dictionary<string, Word>? words = null, uint? memorySize = null) => new(stack ?? this.stack, words ?? this.words, memorySize ?? memory.Size);
}

public static class Stdlib
{
    public static readonly Dictionary<string, Word> words = new(){
        {"emit", new((_, _, _, stack) => {
            var ch = stack.Pop();
            Console.Write(ch < 0 ? (char)0 : ch > 255 ? (char)255 : (char)ch);
            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {".", new Word((_, _, _, stack) => {
            Console.Write(stack.Pop());
            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {"drop", new((_, _, _, stack) => {
            stack.Drop();
            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {"(", new((ctx, words, _, _) => {
            var i = ctx.i;
            var tokens = ctx.tokens;
            var start = i.Value;
            while (i.Value < tokens.Length && tokens[i.Value] != ")") i.Value++;

            if (tokens[i.Value] != ")")
            {
                i.Value = start;
                throw new ForthException("UNTERMINATED COMMENT");
            }

            return null;
        }, ForthStack.IOTypes.Nop)},

        {":", new((ctx, words, _, _) => {
            var i = ctx.i;
            var tokens = ctx.tokens;
            var start = i.Value;
            i.Value++;

            List<string> fnTokens = [];
            while (i.Value < tokens.Length && tokens[i.Value] != ";") {
                if (tokens[i.Value] == ":") throw new ForthException(": INSIDE :");
                fnTokens.Add(tokens[i.Value]);
                i.Value++;
            }
            if (tokens[i.Value] != ";")
            {
                i.Value = start;
                throw new ForthException("UNTERMINATED WORD DECLARATION");
            }
            else if (fnTokens.Count == 0)
                throw new ForthException("NO WORD NAME GIVEN");

            string name = fnTokens[0];
            string effect = "";
            fnTokens = fnTokens[1..];

            if (fnTokens.Count > 0 && fnTokens[0] == "(")
            {
                List<string> effectTokens = [];
                int j = 1;
                while (j < fnTokens.Count && fnTokens[j] != ")")
                {
                    effectTokens.Add(fnTokens[j]);
                    j++;
                }

                if (i.Value >= tokens.Length || fnTokens[j] != ")")
                {
                    i.Value = start + 2;
                    throw new ForthException("UNTERMINATED COMMENT");
                }
                fnTokens = fnTokens[(j + 1)..];
                foreach (string token in effectTokens)
                {
                    effect += " " + token;
                }
                effect = effect.Trim();
            }

            if (words.ContainsKey(name))
            {
                i.Value = start + 1;
                throw new ForthException($"WORD '{name}' ALREADY EXISTS");
            }

            words[name] = new([.. fnTokens], effect);

            return null;
        }, ForthStack.IOTypes.Nop)},

        {"variable", new((ctx, words, _, stack) => {
            var i = ctx.i;
            var tokens = ctx.tokens;
            if (i.Value >= tokens.Length) throw new ForthException("MISSING IDENTIFIER FOR VARIABLE");

            i.Value++;
            string name = tokens[i.Value];

            if (words.ContainsKey(name))
                throw new ForthException($"CANNOT ASSIGN VARIABLE '{name}' TO EXISTING WORD");
            else if (ctx.variables.ContainsKey(name))
                throw new ForthException($"CANNOT ASSIGN VARIABLE '{name}' TO EXISTING VARIABLE");

            i.Value--;
            ctx.variables[name] = stack.Pop();
            i.Value++;

            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {"@", new((ctx, words, _, stack) => {
            var i = ctx.i;
            var tokens = ctx.tokens;
            if (i.Value >= tokens.Length) throw new ForthException("MISSING IDENTIFIER FOR VARIABLE");

            i.Value++;
            string name = tokens[i.Value];

            if (words.ContainsKey(name))
                throw new ForthException($"CANNOT GET VALUE OF WORD '{name}'");
            else if (!ctx.variables.ContainsKey(name))
                throw new ForthException($"CANNOT GET NONEXISTANT VARIABLE '{name}'");

            i.Value--;
            stack.Push(ctx.variables[name]);
            i.Value++;

            return null;
        }, ForthStack.IOTypes.OutputOnly)},

        {"$", new((ctx, words, _, stack) => {
            var i = ctx.i;
            var tokens = ctx.tokens;
            if (i.Value >= tokens.Length) throw new ForthException("MISSING IDENTIFIER FOR VARIABLE");

            i.Value++;
            string name = tokens[i.Value];

            if (words.ContainsKey(name))
                throw new ForthException($"CANNOT SET VALUE OF WORD '{name}'");
            else if (!ctx.variables.ContainsKey(name))
                throw new ForthException($"CANNOT SET NONEXISTANT VARIABLE '{name}'");

            i.Value--;
            ctx.variables[name] = stack.Pop();
            i.Value++;

            return null;
        }, ForthStack.IOTypes.OutputOnly)},

        {"if", new((ctx, _, interpreter, stack) => {
            var i = ctx.i;
            var tokens = ctx.tokens;
            var start = i.Value;
            i.Value++;

            List<string> ifTokens = [];
            int ifnest = 0;
            while (i.Value < tokens.Length)
            {
                if (tokens[i.Value] == "else")
                {
                    if (ifnest == 0) break;
                    ifnest--;
                }
                else if (tokens[i.Value] == ":")
                    throw new ForthException(": INSIDE if ... else");
                else if (tokens[i.Value] == "if")
                    ifnest++;
                ifTokens.Add(tokens[i.Value]);
                i.Value++;
            }

            if (i.Value >= tokens.Length || tokens[i.Value] != "else")
            {
                i.Value = start;
                throw new ForthException("UNTERMINATED IF");
            }

            i.Value++;

            List<string> elseTokens = [];
            int elsenest = 0;
            while (i.Value < tokens.Length)
            {
                if (tokens[i.Value] == "then")
                {
                    if (elsenest == 0) break;
                    elsenest--;
                }
                else if (tokens[i.Value] == ":")
                    throw new ForthException(": INSIDE else ... then");
                else if (tokens[i.Value] == "else")
                    elsenest++;
                elseTokens.Add(tokens[i.Value]);
                i.Value++;
            }

            if (i.Value >= tokens.Length || tokens[i.Value] != "then")
            {
                i.Value = start;
                throw new ForthException("UNTERMINATED ELSE");
            }

            if (stack.Pop().ToForthBool())
                interpreter.Interpret([.. ifTokens], loopIndex: ctx.loopIndex);
            else
                interpreter.Interpret([.. elseTokens],loopIndex: ctx.loopIndex);

            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {"do", new((ctx, _, interpreter, stack) => {
            var i = ctx.i;
            var tokens = ctx.tokens;
            var start = i.Value;
            i.Value++;

            List<string> loopTokens = [];
            int loopnest = 0;
            while (i.Value < tokens.Length)
            {
                if (tokens[i.Value] == "loop")
                {
                    if (loopnest == 0) break;
                    loopnest--;
                }
                if (tokens[i.Value] == ":")
                    throw new ForthException(": INSIDE do ... loop");
                else if (tokens[i.Value] == "do")
                    loopnest++;
                loopTokens.Add(tokens[i.Value]);
                i.Value++;
            }
            if (i.Value >= tokens.Length || tokens[i.Value] != "loop")
            {
                i.Value = start;
                throw new ForthException("UNTERMINATED LOOP");
            }

            var loops = stack.Pop();
            for (int j = 0; j < loops; j++) interpreter.Interpret([.. loopTokens], loopIndex: j);

            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {"index", new((ctx, _, _, stack) => {
            if (ctx.loopIndex == null) throw new ForthException("INDEX NOT ACCESSIBLE OUTSIDE OF LOOP");
            stack.Push(ctx.loopIndex.Value);
            return null;
        }, ForthStack.IOTypes.OutputOnly)},

        {".\"", new((ctx, words, _, _) => {
            var i = ctx.i;
            var tokens = ctx.tokens;
            var start = i.Value;
            i.Value++;

            List<string> stringTokens = [];
            while (i.Value < tokens.Length && tokens[i.Value] != "\"")
            {
                stringTokens.Add(tokens[i.Value]);
                i.Value++;
            }
            if (i.Value >= tokens.Length || tokens[i.Value] != "\"")
            {
                i.Value = start;
                throw new ForthException("UNTERMINATED STRING");
            }

            string output = "";
            foreach (string token in stringTokens) output += " " + token;
            Console.WriteLine(output.Trim());

            return null;
        }, ForthStack.IOTypes.Nop)},

        {"+", new((_, _, _, stack) => {
            int n = stack.Pop();
            stack.Push(stack.Pop() + n);
            return null;
        }, "n n -- n + n")},

        {"-", new((_, _, _, stack) => {
            int n = stack.Pop();
            stack.Push(stack.Pop() - n);
            return null;
        }, "n n -- n - n")},

        {"*", new((_, _, _, stack) => {
            int n = stack.Pop();
            stack.Push(stack.Pop() * n);
            return null;
        }, "n n -- n * n")},

        {"/", new((_, _, _, stack) => {
            int n = stack.Pop();
            stack.Push(stack.Pop() / n);
            return null;
        }, "n n -- n / n")},

        {"mod", new((_, _, _, stack) => {
            int n = stack.Pop();
            stack.Push(stack.Pop() % n);
            return null;
        }, "n n -- n % n")},

        {"=", new((_, _, _, stack) => {
            stack.Push((stack.Pop() == stack.Pop()).ToForthInt());
            return null;
        }, "n n -- n == n")},

        {"!=", new(["=", "invert"], "n n -- n != n")},

        {">", new((_, _, _, stack) => {
            int n = stack.Pop();
            stack.Push((stack.Pop() > n).ToForthInt());
            return null;
        }, "n n -- n > n")},

        {"<", new((_, _, _, stack) => {
            int n = stack.Pop();
            stack.Push((stack.Pop() < n).ToForthInt());
            return null;
        }, "n n -- n < n")},

        {"and", new((_, _, _, stack) => {
            var a = stack.Pop();
            var b = stack.Pop();
            stack.Push((a.ToForthBool() && b.ToForthBool()).ToForthInt());
            return null;
        }, "n n -- n && n")},

        {"or", new((_, _, _, stack) => {
            stack.Push((stack.Pop().ToForthBool() || stack.Pop().ToForthBool()).ToForthInt());
            return null;
        }, "n n -- n || n")},

        {"input", new((_, _, _, stack) => {
            while (true) {
                string input;
                input = Console.ReadLine() ?? "";
                if (input == "")
                {
                    Console.WriteLine("INPUT EMPTY");
                    continue;
                }

                int result;
                try
                {
                    result = Convert.ToInt32(input[0]);
                }
                catch (FormatException)
                {
                    Console.WriteLine("NOT A NUMBER");
                    continue;
                }

                stack.Push(result);
                break;
            }
            return null;
        })},

        {"inputn", new((_, _, _, stack) => {
            while (true)
            {
                string input;
                input = Console.ReadLine() ?? "";
                if (input == "")
                {
                    Console.WriteLine("INPUT EMPTY");
                    continue;
                }

                int result;
                try
                {
                    result = Convert.ToInt32(input);
                }
                catch (FormatException)
                {
                    Console.WriteLine("NOT A NUMBER");
                    continue;
                }

                stack.Push(result);
                break;
            }
            return null;
        })},

        {"bye", new((ctx, _, _, stack) => {
            ctx.i.Value = ctx.tokens.Length - 1;
            return null;
        })},

        {"slen", new((_, _, _, stack) => {
            stack.Push(stack.Count);
            return null;
        }, ForthStack.IOTypes.OutputOnly)},

        {"&", new((ctx, _, interpreter, stack) => {
            interpreter.CheckMMode();
            var memory = ctx.memory;

            return null;
        })},

        {"&=", new((ctx, _, interpreter, stack) => {
            interpreter.CheckMMode();
            var memory = ctx.memory;

            return null;
        })},

        {"empty?", new(["slen", "0="], ForthStack.IOTypes.OutputOnly)},

        {"dup", new(["variable", "a", "@", "a", "@", "a"], "n -- n n")},

        {"!", new(["0", "=", "if", "-1", "else", "0", "then"], "n -- !n")},

        {"cr", new(["10", "emit"])},

        {"bell", new(["7", "emit"])},

        {"nop", new([])},

        {">=", new(["<", "negate"], "n n -- n >= n")},

        {"<=", new([">", "negate"], "n n -- n <= n")},

        {"dump", new(["empty", "if", "else", ".", "cr", "dump", "then"], "n... --")},

        {"0=", new(["0", "="], "n -- n == 0")},

        {"peek", new(["dup", "."], ForthStack.IOTypes.InputOnly)}
    };
}