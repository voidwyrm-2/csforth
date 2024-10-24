namespace CSForth;

class CSForthException(string message) : Exception(message) { }

public readonly struct Word
{
    public readonly string[] words;
    public readonly Func<Common.Ref<int>, string[], Dictionary<string, Word>, Interpreter, ForthStack, string?>? builtin;
    public readonly string effect;

    public Word(string[] words, string effect = "")
    {
        this.words = words;
        builtin = null;
        this.effect = effect;
    }

    public Word(Func<Common.Ref<int>, string[], Dictionary<string, Word>, Interpreter, ForthStack, string?> builtin, string effect = ForthStack.IOTypes.Nop)
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

    public new void Push(int i) => base.Push(i);

    public new int Pop()
    {
        if (Empty()) throw new CSForthException("STACK UNDERFLOW");
        return base.Pop();
    }

    public void Dup()
    {
        int i = Pop();
        Push(i);
        Push(i);
    }

    public void Drop() => Pop();

    public bool Empty() => Count == 0;
}

public class ForthDictionary : Dictionary<string, Word>
{

}

public class Interpreter(ForthStack? stack = null, Dictionary<string, Word>? words = null)
{
    private readonly ForthStack stack = stack ?? [];

    private readonly Dictionary<string, Word> words = words ?? Stdlib.words;

    public void Interpret(string[] tokens)
    {
        //Console.WriteLine(words.Count);
        //foreach (var (k, v) in words) Console.WriteLine(k, v);
        //foreach (var token in tokens) Console.WriteLine(token + "\n");

        for (int i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].TryOutInt(out int integer))
                stack.Push(integer);
            else if (words.TryGetValue(tokens[i], out Word word))
            {
                if (word.builtin != null)
                {
                    var r = new Common.Ref<int>(i);
                    word.builtin(r, tokens, words, this, stack);
                    if (r.Set) i = r.Value == -1 ? tokens.Length : r.Value;
                }
                else
                    Interpret(word.words);
            }
            else
                throw new CSForthException($"UNKNOWN WORD '{tokens[i]}'");
        }
    }

    public void Interpret(string text)
    {
        var tokens = text.Split([' ', '\t', '\n']);
        if (tokens.Contains(""))
        {
            List<string> tokensClean = [];
            foreach (var token in tokens)
            {
                if (token != "") tokensClean.Add(token);
            }
            Interpret([.. tokensClean]);
        }
        else
            Interpret(tokens);
    }
}

public static class Stdlib
{
    public static readonly Dictionary<string, Word> words = new(){
        {"emit", new((_, _, _, _, stack) => {
            Console.Write((char)stack.Pop());
            return null;
        }, ForthStack.IOTypes.InputOnly)},

        { ".", new Word((_, _, _, _, stack) => {
            Console.Write(stack.Pop());
            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {"dup", new((_, _, _, _, stack) => {
            stack.Dup();
            return null;
        }, "n -- n n")},

        {"drop", new((_, _, _, _, stack) => {
            stack.Drop();
            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {"(", new((i, tokens, words, _, _) => {
            while (i.Value < tokens.Length && tokens[i.Value] != ")") i.Value++;

            if (tokens[i.Value] != ")") throw new CSForthException("UNTERMINATED COMMENT");

            return null;
        }, ForthStack.IOTypes.Nop)},

        {":", new((i, tokens, words, _, _) => {
            i.Value++;

            List<string> fnTokens = [];
            while (i.Value < tokens.Length && tokens[i.Value] != ";") {
                if (tokens[i.Value] == ":") throw new CSForthException(": INSIDE :");
                fnTokens.Add(tokens[i.Value]);
                i.Value++;
            }
            if (tokens[i.Value] != ";")
                throw new CSForthException("UNTERMINATED WORD DECLARATION");
            else if (fnTokens.Count == 0)
                throw new CSForthException("NO WORD NAME GIVEN");

            string name = fnTokens[0];
            string effect = "";
            fnTokens = fnTokens[1..];

            if (fnTokens.Count > 0 && fnTokens[0] == "(") {
                List<string> effectTokens = [];
                int j = 1;
                while (j < fnTokens.Count && fnTokens[j] != ")") {
                    effectTokens.Add(fnTokens[j]);
                    j++;
                }

                if (fnTokens[j] != ")") throw new CSForthException("UNTERMINATED COMMENT");
                fnTokens = fnTokens[(j + 1)..];
                foreach (string token in effectTokens) {
                    effect += " " + token;
                }
                effect = effect.Trim();
            }

            if (words.ContainsKey(name))
                throw new CSForthException($"WORD '{name}' ALREADY EXISTS");

            words.Add(name, new([.. fnTokens], effect));

            return null;
        }, ForthStack.IOTypes.Nop)},

        {"if", new((i, tokens, _, interpreter, stack) => {
            i.Value++;

            List<string> ifTokens = [];
            int ifnest = 0;
            while (i.Value < tokens.Length) {
                if (tokens[i.Value] == "else") {
                    if (ifnest == 0) break;
                    ifnest--;
                }
                else if (tokens[i.Value] == ":")
                    throw new CSForthException(": INSIDE if ... else");
                else if (tokens[i.Value] == "if")
                    ifnest++;
                ifTokens.Add(tokens[i.Value]);
                i.Value++;
            }

            if (tokens[i.Value] != "else")
                throw new CSForthException("UNTERMINATED IF");

            i.Value++;

            List<string> elseTokens = [];
            int elsenest = 0;
            while (i.Value < tokens.Length) {
                if (tokens[i.Value] == "then") {
                    if (elsenest == 0) break;
                    elsenest--;
                }
                else if (tokens[i.Value] == ":")
                    throw new CSForthException(": INSIDE else ... then");
                else if (tokens[i.Value] == "else")
                    elsenest++;
                elseTokens.Add(tokens[i.Value]);
                i.Value++;
            }

            if (tokens[i.Value] != "then")
                throw new CSForthException("UNTERMINATED ELSE");

            if (stack.Pop().ToForthBool())
                interpreter.Interpret([.. ifTokens]);
            else
                interpreter.Interpret([.. elseTokens]);

            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {"do", new((i, tokens, _, interpreter, stack) => {
            i.Value++;

            List<string> loopTokens = [];
            int loopnest = 0;
            while (i.Value < tokens.Length) {
                if (tokens[i.Value] == "loop") {
                    if (loopnest == 0) break;
                    loopnest--;
                }
                if (tokens[i.Value] == ":")
                    throw new CSForthException(": INSIDE do ... loop");
                else if (tokens[i.Value] == "do") loopnest++;
                loopTokens.Add(tokens[i.Value]);
                i.Value++;
            }
            if (tokens[i.Value] != "loop")
                throw new CSForthException("UNTERMINATED LOOP");

            var loops = stack.Pop();
            for (int j = 0; j < loops; j++) interpreter.Interpret([.. loopTokens]);

            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {"empty", new((_, _, _, _, stack) => {
            stack.Push(stack.Empty().ToForthInt());
            return null;
        }, ForthStack.IOTypes.OutputOnly)},

        {"invert", new(["0", "=", "if", "-1", "else", "0", "then"], "n -- !n")},

        {"+", new((_, _, _, _, stack) => {
            stack.Push(stack.Pop() + stack.Pop());
            return null;
        }, "n n -- n + n")},

        {"-", new((_, _, _, _, stack) => {
            stack.Push(stack.Pop() - stack.Pop());
            return null;
        }, "n n -- n - n")},

        {"*", new((_, _, _, _, stack) => {
            stack.Push(stack.Pop() * stack.Pop());
            return null;
        }, "n n -- n * n")},

        {"/", new((_, _, _, _, stack) => {
            stack.Push(stack.Pop() / stack.Pop());
            return null;
        }, "n n -- n / n")},

        {"mod", new((_, _, _, _, stack) => {
            stack.Push(stack.Pop() % stack.Pop());
            return null;
        }, "n n -- n % n")},

        {"=", new((_, _, _, _, stack) => {
            stack.Push((stack.Pop() == stack.Pop()).ToForthInt());
            return null;
        }, "n n -- n == n")},

        {"!=", new(["=", "invert"], "n n -- n != n")},

        {">", new((_, _, _, _, stack) => {
            stack.Push((stack.Pop() > stack.Pop()).ToForthInt());
            return null;
        }, "n n -- n > n")},

        {"<", new((_, _, _, _, stack) => {
            stack.Push(stack.Pop() < stack.Pop() ? ForthStack.True : ForthStack.False);
            return null;
        }, "n n -- n < n")},

        {"and", new((_, _, _, _, stack) => {
            stack.Push((stack.Pop().ToForthBool() && stack.Pop().ToForthBool()).ToForthInt());
            return null;
        }, "n n -- n && n")},

        {"or", new((_, _, _, _, stack) => {
            stack.Push((stack.Pop().ToForthBool() || stack.Pop().ToForthBool()).ToForthInt());
            return null;
        }, "n n -- n || n")},

        {"input", new((_, _, _, _, stack) => {
            while (true) {
                string input;
                input = Console.ReadLine() ?? "";
                if (input == "") {
                    Console.WriteLine("INPUT EMPTY");
                    continue;
                }

                int result;
                try {
                    result = Convert.ToInt32(input[0]);
                } catch (FormatException) {
                    Console.WriteLine("NOT A NUMBER");
                    continue;
                }

                stack.Push(result);
                break;
            }
            return null;
        })},

        {"inputn", new((_, _, _, _, stack) => {
            while (true) {
                string input;
                input = Console.ReadLine() ?? "";
                if (input == "") {
                    Console.WriteLine("INPUT EMPTY");
                    continue;
                }

                int result;
                try {
                    result = Convert.ToInt32(input);
                } catch (FormatException) {
                    Console.WriteLine("NOT A NUMBER");
                    continue;
                }

                stack.Push(result);
                break;
            }
            return null;
        })},

        {"cr", new(["10", "emit"], ForthStack.IOTypes.Nop)},

        {"nop", new([])},

        {">=", new(["<", "negate"], "n n -- n >= n")},

        {"<=", new([">", "negate"], "n n -- n <= n")},

        //{"bell", new(["7", "emit"], ForthStack.IOTypes.InputOnly)},
    };
}