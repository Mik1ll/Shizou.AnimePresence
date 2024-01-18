// See https://aka.ms/new-console-template for more information

using Shizou.MpvDiscordPresence;

using var client = new MpvPipeClient("tmp/mpvsocket");

while (true)
{
    var key = Console.ReadLine() ?? "";
    var result = client.GetPropertyString(key);
    Console.WriteLine(result);
}
