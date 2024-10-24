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
            List<string> fnTokens = [];
            i.Value++;
            while (i.Value < tokens.Length && tokens[i.Value] != ";") {
                if (tokens[i.Value] == ":") throw new CSForthException(": INSIDE :");
                fnTokens.Add(tokens[i.Value]);
                i.Value++;
            }
            if (tokens[i.Value] != ";")
                throw new CSForthException("UNTERMINATED COMMENT");
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
            List<string> ifTokens = [];
            i.Value++;
            while (i.Value < tokens.Length && tokens[i.Value] != "else") {
                if (tokens[i.Value] == ":") throw new CSForthException(": INSIDE if");
                ifTokens.Add(tokens[i.Value]);
                i.Value++;
            }

            if (tokens[i.Value] != "else")
                throw new CSForthException("UNTERMINATED IF");

            i.Value++;

            List<string> elseTokens = [];
            while (i.Value < tokens.Length && tokens[i.Value] != "then") {
                if (tokens[i.Value] == ":") throw new CSForthException(": INSIDE else");
                elseTokens.Add(tokens[i.Value]);
                i.Value++;
            }

            if (tokens[i.Value] != "then")
                throw new CSForthException("UNTERMINATED ELSE");

            if (stack.Pop() != 0)
                interpreter.Interpret([.. ifTokens]);
            else
                interpreter.Interpret([.. elseTokens]);

            return null;
        }, ForthStack.IOTypes.InputOnly)},

        {"empty", new((_, _, _, _, stack) => {
            stack.Push(stack.Empty() ? 1 : 0);
            return null;
        }, ForthStack.IOTypes.OutputOnly)},

        {"invert", new(["0", "=", "if", "-1", "else", "0", "then"], "n -- !n")},

        {"=", new((_, _, _, _, stack) => {
            stack.Push(stack.Pop() == stack.Pop() ? ForthStack.True : ForthStack.False);
            return null;
        }, "n n -- n == n")},

        {"!=", new(["=", "invert"], "n n -- n != n")},

        {"cr", new(["10", "emit"], ForthStack.IOTypes.Nop)},

        {"nop", new([])},

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
        })}

        //{"bell", new(["7", "emit"], ForthStack.IOTypes.InputOnly)},
    };
}