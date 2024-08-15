# Microsoft Content Credentials SDK

This document serves as a guide to using the Microsoft Content Credentials Software Development Kit for C2PA signing!

## Structure

Currently, the SDK is structured into the following files, all under the C2pa namespace:

- C2pa.cs
- Assertions.cs
- C2paException.cs
- Manifest.cs
- ManifestBuilder.cs
- Utils.cs

### C2pa.cs

This file contains the following classes:

- StreamAdapter:    Class used to create a `C2paStream`, for passing `Stream` pointers to the C / Rust code.
- ISignerCallback:  An interface that when implemented defines an object suitable to be passed to a ManifestBuilder for signing. The implemented class must also define the sign function to be used for signing.
- SignerConfig:     A class that defines the configurations needed for a signer, such as the algorithm, certifications, time authority and whether to use Ocsp or not.
- Sdk:              This is a static class that provides meta information about the SDK such as Version and the Supported File Extensions.

### Assertions.cs

This file contains some of the different types of assertions supported by the c2pa standard, along with the option to define a custom assertion. The file contains the following classes:

- AssertionKind:            An Enum defining the 2 different kings of assertions, those being Cbor or Json.
- AssertionTypeConverter:   A custom Json Converter that is used to serialize and deserialize assertions.
- Assertion:                The base class that all other Assertions derive from. If making a reusable custom assertion, it *MUST* inherit from this class.
- AssertionData:            The base class for data used by Assertions. If making a reusable custom assertion data defintion, it *MUST* inherit from this class.

The other classes in this file represent predefined Assertion types, and their associated data types, such as the `CreativeWorkAssertion` and its associated `CreativeWorkAssertionData`.

#### Note

As mentioned, if you wish to expand the predefined list, the assertion class you create should inherit from `Assertion` and its data should inherit from `AssertionData`.
When your class inherits from `Assertion`, it must also pass a unique label, along with its data class that inherits from `AssertionData`.
The custom label should then be added to the `Utils.cs` file in the `GetAssertionTypeFromLabel` where it should match to its `Assertion` type.
If the label is not added to this function, then it will default to an instance of the `CustomAssertion` class, whose data will be serialized to a dynamic ExpandoObject.

### C2paException.cs

This file holds the C2pa Exception class. Originally, I was planning on making use of a factory and making multiple types of assertions based on the errors received from the Rust code if any were to occur during signing.
But decided that just a `C2paException` was sufficient.

### Manifest.cs

This file contains the core classes used by the `Manifest` and `ManifestDefinition`, which are the building blocks to signing. These classes are mainly C# versions of those existing in the Rust code, and are as follows:

- Thumbnail: Used to represent a Thumbnail.
- ResourceRef: Used to represent a reference to a resource.
- ResourceStore: Used to represent a store for resources.
- Relationship: Used to define the relationship `ingredient`s can have with each other.
- Ingredient: This represents an ingredient. This will be information about a file that is used to create another file.
- Manifest: This represents a manifest as received from a signed file. (Note, this class needs to be expanded with more properties to deserialize everything.)
- ManifestDefinition: This represents the manifest used to sign a file.
- ManifestStore: Represents a manifest store as received and deserialized from a signed file. This contains a mapping of all `Manifest`s on the file, along with a direct reference to the most recent one under `ActiveManfiest`.
- ManfiestStoreReader: Helper class used to read a manifest store from a signed file.

### ManifestBuilder.cs

This file contains the manifest builder, a class used to incrementally build a `ManifestDefinition`, which is in turn used to sign the file using the `sign` method of the builder. Apart from the `ManifestBuilder`, this file also contains the definition for the `ManifestBuilderSettings` class.

### Utils.cs

This file contains the `Utils` class containing a few utility functions used by the Microsoft Content Credentials Dotnet SDK.
