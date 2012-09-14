using System;

namespace BootSwitchSvc
{
    public class SpecialFolderPatternConverter : log4net.Util.PatternConverter
    {
        override protected void Convert(System.IO.TextWriter writer, object state)
        {
            Environment.SpecialFolder f =
                (Environment.SpecialFolder)Enum.Parse(typeof(Environment.SpecialFolder),
                    base.Option, true);
            writer.Write(Environment.GetFolderPath(f));
        }
    }
}
