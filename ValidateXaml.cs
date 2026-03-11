using System;
using System.Xml;

namespace XamlValidator
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(@"Management.Presentation\Views\Restaurant\RestaurantOrderingView.xaml");
                Console.WriteLine("XAML is valid!");
            }
            catch (XmlException ex)
            {
                Console.WriteLine($"XML Error at Line {ex.LineNumber}, Position {ex.LinePosition}: {ex.Message}");
            }
        }
    }
}
