using Sample;
using Helper = Sample.FileHelper;

var helper = new FileHelper();
Helper aliased = helper;
var tempHelper = new FileHelper(@"C:\temp");
Console.WriteLine(helper.Root);

helper.Copy("a.txt", "b.txt");
helper.Copy("a.txt", "b.txt", true);
Console.WriteLine(helper.Exists("a.txt"));
helper.Move("a.txt", "b.txt");
var lines = helper.ReadLines("a.txt");
int x = FileHelper.Map<int>(1);
Console.WriteLine("hello".Shout());

helper.Delete("a.txt");
helper.Rename("a.txt", "b.txt");
var archived = helper.Archive("a.txt");
var found = helper.FindAll("data");
helper.Migrate("data");
helper.Append("log.txt", "started");
string text = await helper.ReadAsync("a.txt", CancellationToken.None);
int copied = helper.CopyAll("src", "dest", "*.txt");
using var stream = helper.OpenWrite("a.txt");

var cache = new FileCache<string>();
cache.Set("a.txt", "contents");

var target = CopyTarget.Backup;
Console.WriteLine(target);
