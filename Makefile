OS := $(shell uname)
CFLAGS = -I. -Wall 
ifeq ($(OS), Darwin)
CFLAGS += -framework Security
endif
ifeq ($(OS), Linux)
CFLAGS = -pthread -Wl,--no-as-needed -ldl -lm
endif

# default version of python is 3.11
PYTHON=python3.12
LIBRARY=libc2pa_bindings.so

release: 
	cargo build --release --features=uniffi/cli

c_bindings: release
	cbindgen --config cbindgen.toml --crate c2pa-bindings --output target/c2pa.h --lang c

test_c: c_bindings
	$(CC) $(CFLAGS) -I target tests/c/main.c -o target/ctest -lc2pa_bindings -L./target/release 
	LD_LIBRARY_PATH=target/release target/ctest

python: release
	cargo run --release --features=uniffi/cli --bin uniffi_bindgen generate src/c2pa.udl -n --language python -o target/python
	cp target/release/$(LIBRARY) target/python/

swift: release
	cargo run --release --features=uniffi/cli --bin uniffi_bindgen generate src/c2pa.udl -n --language swift -o target/swift
	cp target/release/$(LIBRARY) target/swift/

test_python: python
	$(PYTHON) tests/python/test.py

dotnet: c_bindings
	dotnet run --project tests/dotnet/generator/generator.csproj

test_dotnet: dotnet
	LD_LIBRARY_PATH=target/release RUST_BACKTRACE=full  dotnet run --project tests/dotnet/test/test.csproj

test: test_python test_c

