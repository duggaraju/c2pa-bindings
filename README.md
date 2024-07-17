# c2pa_bindings

A reworked c2pa sdk that can be shared across many programming languages and platforms.

This uses the uniffi tools to create language bindings for the Rust c2pa-rs library.

The result is a dynamic library that can be called directly from python and from c and eventually from swift and kotlin

This is a VERY experimental work in progress and will change considerably

Adding Manifests does not work here yet

# Building the dynamic library only

Run `make release`

# Building for python bindings

Run `make python`

# Testing C

Run `make test_c`

# Testing python

Run `make test_python`

# Testing all

Run `make test`


# Note for ssl functionality you may need to update the python ssl package

`pip install pyopenssl cryptography --upgrade`

# Dotnet

## Building

Setting up Dotnet on Windows can be done by following these steps:

1. Open a terminal in this folder (the root)
2. Referring to the Makefile, run:

    ```sh
    cargo build --release --features=uniffi/cli`
    cargo install cbindgen
    cbindgen --config cbindgen.toml --crate c2pa-bindings --output target/c2pa.h --lang c
    dotnet run --project tests/dotnet/generator/generator.csproj
    ```

3. Next, build the project.
