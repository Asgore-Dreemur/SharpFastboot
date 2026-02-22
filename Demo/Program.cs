using LibUsbDotNet.LibUsb;

namespace Demo
{
    static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("--- IUsbDevice Properties ---");
            foreach (var prop in typeof(IUsbDevice).GetProperties().OrderBy(p => p.Name))
                Console.WriteLine($"Prop: {prop.Name} ({prop.PropertyType.Name})");

            Console.WriteLine("\n--- UsbEndpointReader Members ---");
            foreach (var prop in typeof(UsbEndpointReader).GetProperties().OrderBy(p => p.Name))
                Console.WriteLine($"Prop: {prop.Name} ({prop.PropertyType.Name})");
            foreach (var method in typeof(UsbEndpointReader).GetMethods().OrderBy(m => m.Name))
            {
                var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"Method: {method.Name}({parameters})");
            }

            Console.WriteLine("\n--- UsbEndpointWriter Members ---");
            foreach (var prop in typeof(UsbEndpointWriter).GetProperties().OrderBy(p => p.Name))
                Console.WriteLine($"Prop: {prop.Name} ({prop.PropertyType.Name})");
            foreach (var method in typeof(UsbEndpointWriter).GetMethods().OrderBy(m => m.Name))
                Console.WriteLine($"Method: {method.Name}");
        }
    }
}