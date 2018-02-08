using Cauldron.Consoles;
using System;

namespace HostsBlockUpdater
{
    /// <summary>
    /// Implements the <see cref="HostsUpdater" /> class
    /// </summary>
    public sealed class HostsUpdater
    {
        /// <summary>
        /// The Main method is the entry point
        /// </summary>
        /// <param name="args">Command parameters</param>
        public static void Main(string[] args)
        {
            var parser = new ParameterParser(new MainExecutionGroup(), new InstallationExecutionGroup())
            {
                DescriptionColor = ConsoleColor.Cyan
            };

            try
            {
                parser.Parse(args);
                if (!parser.Execute())
                    parser.ShowHelp();
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Message);
                Console.ResetColor();
            }
        }
    }
}