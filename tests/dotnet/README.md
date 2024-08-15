# C2PA Dotnet SDK

This document serves as a general guide for developers looking to work on the Dotnet bindings for Rust C2PA SDK.

## Getting Setup

To get setup on windows, open your terminal, and navigate to the root of this workspace (../../../)
Note, this assumes that you have Rust and the Dotnet framework installed and setup.
From there, run the following commands:

```sh
cargo build --release --features=uniffi/cli # Builds the rust code.
cbindgen --config cbindgen.toml --crate c2pa-bindings --output target/c2pa.h --lang c # Generates the C header file from the built rust code
dotnet run --project tests/dotnet/generator/generator.csproj # Generates the C# library from the C header file.
```

From there, the contents within tests/dotnet/ are available to you to use.

## Project Structure

The tests/dotnet folder consists of the following folders:

- ContentCredentialSigner
- generator
- sample
- sdk
- sdktests
- uffi

### ContentCredentialSigner/

This folder holds the sample code for the Azure Functions created using the SDK defined in the `sdk` folder. It serves to provide examples of how the SDK can be used in various contexts as an Azure function to accomplish different tasks.

### generator/

As indicated from the setup commands, this folder holds the code used to generate the C# bindings from the `c2pa.h` header file.

### sample/

Similar to ContentCredentialSigner, this folder contains sample code in the form of a command line interface (CLI), making use of the SDK defined in the `sdk` folder. It serves to provide examples of how the SDK can be consumed, and objects structured.

### sdk/

This folder contains the various files that make up the Dotnet Software Development Kit (SDK), and is where the source code is defined.

### sdktests/

This folder contains all the testing code used to test the sdk as defined in the `sdk` folder.

### uffi/

This folder was originally created to do the bindings of C# to Rust directly without having to go through C, however we ran into some issues, and thus this folder remains unused. However, since this would be ideal, instead of relying on an intemediary step, we hope this functionality is eventually implemented.

### test/

The test folder is unused and can be safely ignored or removed.

## Contributing

Contributing to this project: *Insert contributing and license information here*
