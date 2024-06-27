# C# C2PA SDK Sample

This file exists to provide sample documentation and code on how to use the C# C2PA SDK for the signing and content integrity of media files.

## About the SDK

This C# SDK is meant to serve as a wrapper for the Rust Implmentation of the [C2PA Specification](https://c2pa.org/specifications/specifications/1.3/index.html). This SDK is designed to serve as an interface for other developers and processes to interact with the SDK using C#.

I recommend reading the [Content Authenticity Initiative](https://opensource.contentauthenticity.org/docs/getting-started/) for a better understanding of the underlying information before using this service.

## Features

Currently, the C# SDK is capable of doing the following:

- Signing Media
- Reading manifests from media

Each of which have examples below and in the specified files.

### Signing Media

Using the C2PA specification, users will be able to sign a media file (image, video, pdf). This will put what's known as a [manifest](https://opensource.contentauthenticity.org/docs/manifest/understanding-manifest) on the file, which can then be used to track any subsequent changes made, and can be used to verify the integrity of the file.

For more information about the manifest, refer to this [CIA C2PA Manifest website](https://opensource.contentauthenticity.org/docs/manifest/understanding-manifest/)

There are several methods for accessing certificates, which may change the implementation of the underlying code, and will be up to the developer's discretion.

Signing using the SDK requires the following objects:

- ManifestBuilder: This is the builder used to fully create the manifest that will be responsible for signing the media file.
- Manifest: This is a class representing the structure of a manifest definition, that the Manifest Builder will use.
- ISignerCallback: This is the Interface that must be implemented, that will provide the Config and Sign method for the Manifest Builder.

Using the SDK to sign, requires that you, the developer, implement the ISignerCallback interface, create a Manifest object, then make use of the ManifestBuilder's Sign method to sign your files.

The ISignerCallback interface has 2 required methods, these being a `Sign` method, which accepts a ReadOnlySpan of bytes `ReadOnlySpan<byte>`, containing the data to be signed, and a Span of type byte `Span<byte>` which will be the location where the signed version of your data will be stored. The second required method of the ISignerCallback, is a method called `Config`, which returns a `SignerConfig` object.

Example of implementing the ISignerCallback Interface:

```c#
    internal class Program {
        
        // Having a callback class implementing the ISignerCallback Interface.
        public class MyCallback : ISignerCallback 
        {
            public int Sign(ReadOnlySpan<byte> data, Span<byte> hash)
            {
                // Implement your custom signing logic here
                // ...
                // ...
                // ...
                // Copy the signature to the hash span
                // hash = signature;

                return hash.Length; // Return the length of the signature
            }
            
            public SignerConfig Config => new SignerConfig
            {
                Alg = "your-signing-algorithm",
                Certs = "Your Certificate file",
                TimeAuhorityUrl = "URL For some Time Authority. Example: http://timestamp.digicert.com",
                UseOcsp = bool
            };
        }

        // Driver Code
        public static void Main(string[] args){
            string pathToFileToSign = "some/path/to/media/file.png";
            string outputPath = "path/where/signed/file/goes.png";

            // Can get your signer first.
            MyCallback signer = new();

            // Need your ManifestBuilderSettings which has the name of your ClaimGenerator:
            ManifestBuilderSettings builderSettings = new ()
            {
                ClaimGenerator = "Your Claim Generator Name Here"
            };

            // Next, we provide a manifest object so that creating your manifest definition will be less error prone.
            Manifest manifest = new ()
            {
                Title = "Title of Manifest",
                Format = "jpg",
                ClaimGeneratorInfo = [new("Claim Generator Name here again", "Version")],
                Assertions = [ new("Label for your assertion", new AssertionData("Your Assertion Context url", "The type of assertion", [new AuthorInfo("Type of Author", "Name of Author")]))]
            };

            // Can now create the builder using the ManifestBuilder class.
            ManifestBuilder builder = new(builderSettings, signer.Config, signer, manifest.GetManifestJson());

            // Lastly, can sign by using the Builder's sign method.
            builder.Sign(pathToFileToSign, outputPath);

            // Once all goes well, file should be signed. 
        }
    }
```

Any errors that occur during the signing process, will raise a `C2paExceptions.C2paException` exception, that will contain more information about what went wrong.

### Reading Signatures From Media

Once you have your media signed, there will come a time when you will want to read that signature, or even the signature of other files you have obtaiuned, to ensure their validity. To do this we will make use of the provided ManifestReader class.

Using this class, you may provide the filename of the file whose manifest you wish to read. Once the file has a manifest attached to it, it will be returned in JSON form. If need be, you may also make use of the Manifest class to serialize the returned JSON into a Manifest Object to perform your validation or checking.

Example:

```c#
    Console.WriteLine("Some Demo Here");
```
