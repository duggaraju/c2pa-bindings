// See https://aka.ms/new-console-template for more information

using uniffi.c2pa;

var certChain = "/home/krishndu/rus/c2pa-rs/sdk/tests/fixtures/certs/es384.pub";
var keyFile = "/home/krishndu/rus/c2pa-rs/sdk/tests/fixtures/certs/es384.pub";

var stream = new FileStream("/mnt/c/Users/krishndu/Pictures/thumbnail_image005.png");
var reader = new ManifestStoreReader();
var json = reader.ReadStream("image/png", stream);
Console.WriteLine("JSON is {0}", json);
var callback = new Signer();
C2paSigner signer = new C2paSigner(callback);
