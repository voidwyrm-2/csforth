using Argsharp;

namespace CSForth;
class Program
{
    private const string CSForthVersion = "1.0.0";

    static void Main(string[] args)
    {
        Flag help = new("h", "help", "Lists the commands");
        Flag version = new("v", "version", "Shows the current CSForth version");

        Parser parser = new(args, [help, version], "csforth", "The CSForth interpreter", ["path"]);

        ResultMap parsed;
        string[] leftover;
        try
        {
            (parsed, leftover) = parser.Parse();
        }
        catch (ArgsharpParseException e)
        {
            Console.WriteLine(e);
            return;
        }

        if (parsed.TryFlag(help))
        {
            Console.WriteLine(parser.Help());
            return;
        }
        else if (parsed.TryFlag(version))
        {
            Console.WriteLine("CSForth Interpreter version " + CSForthVersion);
            return;
        }

        if (leftover.Length == 0)
        {
            Console.WriteLine("expected: " + parser.Usage());
            return;
        }

        var path = leftover[0];

        string content;
        try
        {
            content = File.ReadAllText(path);
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"path {path} cannot be found");
            return;
        }

        Interpreter interpreter = new();
        try
        {
            interpreter.Interpret(content);
        }
        catch (CSForthException e)
        {
            Console.WriteLine(e);
        }
    }
}