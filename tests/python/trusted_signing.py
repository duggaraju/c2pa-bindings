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
# each license.import os

import sys
import os
import json
PROJECT_PATH = os.getcwd()
SOURCE_PATH = os.path.join(
    PROJECT_PATH,"target","python"
)
sys.path.append(SOURCE_PATH)

import c2pa_api


class TrustedSigner:

    def __init__(self, config, sign_callback):
        self.credentials = DefaultAzureCredential(exclude_interactive_browser_credential = True)
        callback = c2pa_api.SignerCallback(sign_callback)
        self.signer = c2pa_api.C2paSigner(callback)
        self.signer.configure(config)

    def signer(self):
        return self.signer
    
    def sign(data):
        accountName = 'accountName'
        profileName = 'profileName'
        # use urllib to post to sign the data and return the signed data.

    
    def get_certs():
        # Make API call to get certs. Use a dummy random data to make the API call.
        return '' 

    def default(credentials, url):
        certs = TrustedSigner.get_certs()
        config = c2pa.SignerConfig(alg = 'ps384', certs = certs, time_authority_url = 'https://timestamp.digicert.com', use_ocsp = False)
        return TrustedSignerSigner(config, sign_callback).signer

# paths to our certs
pemFile = os.path.join(PROJECT_PATH,"tests","fixtures","ps256.pub")
keyFile = os.path.join(PROJECT_PATH,"tests","fixtures","ps256.pem")
# path to a file that already has a manifest store for reading
testFile = os.path.join(PROJECT_PATH,"tests","fixtures","C.jpg")

# Output files (ensure they do not exist)
outFile = os.path.join(PROJECT_PATH,"target","python_out.jpg")
if os.path.exists(outFile):
    os.remove(outFile)
thumb_file = os.path.join(PROJECT_PATH,"target","thumb_from_python.jpg")
if os.path.exists(thumb_file):
    os.remove(thumb_file)


# example of reading a manifest store from a file
try:
    reader = c2pa_api.ManifestStoreReader.from_file(testFile)
    jsonReport = reader.read()
    print(jsonReport)
except Exception as e:
    print("Failed to read manifest store: " + str(e))
    exit(1)


try:
    # now if we want to read a resource such as a thumbnail from the manifest store
    # we need to find the id of the resource we want to read
    report = json.loads(jsonReport)
    manifest_label = report["active_manifest"]
    manifest = report["manifests"][manifest_label]
    thumb_id = manifest["thumbnail"]["identifier"]
    # now write the thumbnail to a file
    reader.resource_to_file(manifest_label, thumb_id, thumb_file)
except Exception as e:
    print("Failed to write thumbnail: " + str(e))
    exit(1)

print("Thumbnail written to " + thumb_file)

# Define a manifest as a dictionary
manifestDefinition = {
    "claim_generator": "python_test",
    "claim_generator_info": [{
        "name": "python_test",
        "version": "0.0.1",
    }],
    "format": "image/jpeg",
    "title": "Python Test Image",
    "ingredients": [],
    "assertions": [
        {   'label': 'stds.schema-org.CreativeWork',
            'data': {
                '@context': 'http://schema.org/',
                '@type': 'CreativeWork',
                'author': [
                    {   '@type': 'Person',
                        'name': 'Gavin Peacock'
                    }
                ]
            },
            'kind': 'Json'
        }
    ]
 }

# Create a trusted signer 
signer = TrustedSigner.default('https://eus.codesigning.azure.net/')

# Example of signing a manifest store into a file
try:
    settings = c2pa_api.c2pa.ManifestBuilderSettings(generator = "python-generator", settings = r"""
    {
        "trust" : {
            "trust_config": "1.3.6.1.5.5.7.3.36\n1.3.6.1.4.1.311.76.59.1.9"
        }
    }
    """) 
    c2pa_api.ManifestBuilder.sign_with_files(settings, signer, manifestDefinition, testFile, outFile)
    #builder = c2pa_api.ManifestBuilder(settings, signer, manifestDefinition)
    #c2pa_api.ManifestBuilder.sign(testFile, outFile) 

except Exception as e:
    print("Failed to sign manifest store: " + str(e))
    exit(1)

print("manifest store written to " + outFile)
print(c2pa_api.ManifestStoreReader.from_file(outFile).read())