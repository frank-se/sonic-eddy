using System.Text.Json;

Console.WriteLine("LV2 info");

Fr.Lv2.Lv2.Init();

var json = Fr.Lv2.Lv2.ClassDescriptions();

Console.WriteLine(JsonSerializer.Serialize(json));

Fr.Lv2.Lv2.Destroy();