using System.IO;

namespace server.app
{
    internal class init : RequestHandler
    {
        // none?
        protected override void HandleRequest()
        {
            WriteLine(File.ReadAllText("init.txt"));
        }
    }
}