# Copyright 2023 Adobe. All rights reserved.
# This file is licensed to you under the Apache License,
# Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
# or the MIT license (http://opensource.org/licenses/MIT),
# at your option.

# Unless required by applicable law or agreed to in writing,
# this software is distributed on an "AS IS" BASIS, WITHOUT
# WARRANTIES OR REPRESENTATIONS OF ANY KIND, either express or
# implied. See the LICENSE-MIT and LICENSE-APACHE files for the
# specific language governing permissions and limitations under
# each license.

import json
import os
import sys
PROJECT_PATH = os.getcwd()
SOURCE_PATH = os.path.join(
    PROJECT_PATH,"target","python"
)
sys.path.append(SOURCE_PATH)

import c2pa;

#  ManifestStoreReader = c2pa.ManifestStoreReader
class ManifestStoreReader(c2pa.ManifestStoreReader):
    def __init__(self, format, stream):
        self.format = format
        self.stream = C2paStream(stream)
        super().__init__()

    def from_file(path: str, format=None):
        file = open(path, "rb")
        if format is None:
            # determine the format from the file extension
            format = os.path.splitext(path)[1][1:]
        reader = ManifestStoreReader(format, file)
        #reader.read() # read the manifest
        return reader

    def read(self):
        return super().read_stream(self.format, self.stream)
    
    def resource_to_stream(self, manifest_label, resource_id, stream) -> None:
        super().resource_write(manifest_label, resource_id, C2paStream(stream))

    def resource_to_file(self, manifest_label, resource_id, path) -> None:
        file = open(path, "wb")
        self.resource_to_stream(manifest_label, resource_id, file)

# Implements a C2paStream given a stream handle
class C2paStream(c2pa.Stream):
    def __init__(self, stream):
        self.stream = stream
    
    def read_stream(self, length: int) -> bytes:   
        return self.stream.read(length)

    def seek_stream(self, pos: int, mode: c2pa.SeekMode) -> int:
        whence = 0
        if mode is c2pa.SeekMode.CURRENT:
            whence = 1
        elif mode is c2pa.SeekMode.END:
            whence = 2
        #print("Seeking to " + str(pos) + " with whence " + str(whence))
        return self.stream.seek(pos, whence)

    def write_stream(self, data: str) -> int:
        return self.stream.write(data)

    def flush_stream(self) -> None:
        self.stream.flush()

    # A shortcut method to open a C2paStream from a path/mode
    def open_file(path: str, mode: str) -> c2pa.Stream:
        return C2paStream(open(path, mode))


class SignerCallback(c2pa.SignerCallback):
    def __init__(self):
        pass

    def sign(self, data: bytes) -> bytes:
        open("data.bin", "wb").write(data)
        os.system("openssl dgst -sha256 -sign tests/fixtures/ps256.pem -out signature.sig data.bin")
        return open("signature.sig", "rb").read()

class LocalSigner:

    def __init__(self, config):
        callback = SignerCallback()
        self.signer = c2pa.C2paSigner(callback)
        self.signer.configure(config)

    def signer(self):
        return self.signer
    
    def from_settings(alg, certs_path, timestamp_url=None):
        certs = open(certs_path,"rb").read()
        config = c2pa.SignerConfig(alg, certs, timestamp_url)
        return LocalSigner(config).signer

class ManifestBuilder(c2pa.ManifestBuilder):
    def __init__(self, settings):
        super().__init__(settings)

    def sign_with_files(settings, signer, manifestJson, sourcePath, outputPath):
        builder = c2pa.ManifestBuilder(settings)
        builder.from_json(json.dumps(manifestJson))
        input = C2paStream.open_file(sourcePath, "rb")
        output = C2paStream.open_file(outputPath, "wb")
        builder.sign_stream(signer, input, output)
        return builder

 
class Manifest:
    def __init__(self, title, format, claim_generator, thumbnail, ingredients, assertions, sig_info=None):
        self.title = title
        self.format = format
        self.claim_generator = claim_generator
        self.thumbnail = thumbnail
        self.ingredients = ingredients
        self.assertions = assertions
        self.signature_info = sig_info

    

class ManifestStore:
    def __init__(self, activeManifest, manifests, validationStatus=None):
        self.activeManifest = activeManifest
        self.manifests = manifests
        self.validationStatus = validationStatus
        
    def __str__(self):
        return json.dumps(dict(self), ensure_ascii=False)
    
    @staticmethod
    def from_json(json_str):
        json_dct = json.loads(json_str)
        manifests = {}
        for label, manifest in json_dct["manifests"].items():
            manifests[label] = Manifest(
                manifest.get("title"),
                manifest.get("format"),
                manifest.get("claim_generator"),
                manifest.get("thumbnail"),
                manifest.get("ingredients"),
                manifest.get("assertions"),
                manifest.get("signature_info")
            )

        return ManifestStore(json_dct['active_manifest'],
                manifests, json_dct.get('validation_status'))

__all__ = ["C2paStream", "Manifest", "ManifestStore", "ManifestStoreReader"]