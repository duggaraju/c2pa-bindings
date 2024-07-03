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

import base64
import c2pa_api
import sys
import os
import json
from azure.identity import DefaultAzureCredential
from requests import get, post
from OpenSSL import crypto
import subprocess
import tempfile
import traceback
import time
import hashlib

import requests
PROJECT_PATH = os.getcwd()
SOURCE_PATH = os.path.join(
    PROJECT_PATH, "target", "python"
)
sys.path.append(SOURCE_PATH)


accountName = 'ts-80221a56b4b24529a43e'
profileName = 'media-provenance-sign'


class TrustedSigner:

    def __init__(self, credentials, url, config):
        self.credentials = credentials
        self.url = url
        callback = c2pa_api.SignerCallback(
            lambda data: self.sign_callback(data))
        self.signer = c2pa_api.c2pa.C2paSigner(callback)
        self.signer.configure(config)

    def signer(self):
        return self.signer

    def sign_callback(self, data):
        # Make API call to sign the data and return the signed data.
        # get the SHA-384 digest of the data.
        digest = hashlib.sha384(data).digest()
        token = self.credentials.get_token('https://codesigning.azure.net')
        # make a call to get the certs
        data = {
            'signatureAlgorithm': 'ps384',
            'digest': base64.b64encode(digest).decode()
        }
        request = post(self.url + f'/codesigningaccounts/{accountName}/certificateprofiles/{profileName}/sign?api-version=2022-06-15-preview',
                    headers={'Authorization': 'Bearer ' + token.token, 'Content-Type': 'application/json'}, json=data)
        print(request.text)
        request.raise_for_status()
        json = request.json()
        while json['status'] == 'InProgress':
            time.sleep(0.300)
            request = get(self.url + f'/codesigningaccounts/{accountName}/certificateprofiles/{profileName}/sign/{json['operationId']}?api-version=2022-06-15-preview',
                          headers={'Authorization': 'Bearer ' + token.token})
            request.raise_for_status()
            print(request.text)
            json = request.json()

        return base64.b64decode(json['signature'])

    @staticmethod
    def get_certs(credentials, url):
        # Make API call to get certs. Use a dummy random data to make the API call.
        token = credentials.get_token('https://codesigning.azure.net')
        # make a call to get the certs
        request = get(url + '/codesigningaccounts/ts-80221a56b4b24529a43e/certificateprofiles/media-provenance-sign/sign/certchain?api-version=2022-06-15-preview',
                      headers={'Authorization': 'Bearer ' + token.token})
        request.raise_for_status()
        # base64 encode p7b_data
        p7b_data = '-----BEGIN PKCS7-----\n' + \
            base64.b64encode(request.content).decode() + \
            "\n-----END PKCS7-----"
        p7b_file = tempfile.NamedTemporaryFile(
            mode='w', suffix='.p7b', delete_on_close=False)
        p7b_file.write(p7b_data)

        pem_file = tempfile.NamedTemporaryFile(mode='w', suffix='.pem')
        # convert p7b to pem
        subprocess.run(f'openssl pkcs7 -print_certs -in {p7b_file.name} -out {
                       pem_file.name}', shell=True, check=True)
        with open(pem_file.name, 'rb') as file:
            pem = file.read()
            return pem

    @staticmethod
    def default():
        credentials = DefaultAzureCredential(
            exclude_interactive_browser_credential=True)
        url = 'https://eus.codesigning.azure.net'
        certs = TrustedSigner.get_certs(credentials, url)
        config = c2pa_api.c2pa.SignerConfig(
            alg='ps384', certs=certs, time_authority_url='http://timestamp.digicert.com', use_ocsp=False)
        return TrustedSigner(credentials, url, config)


# paths to our certs
pemFile = os.path.join(PROJECT_PATH, "tests", "fixtures", "ps256.pub")
keyFile = os.path.join(PROJECT_PATH, "tests", "fixtures", "ps256.pem")
# path to a file that already has a manifest store for reading
testFile = os.path.join(PROJECT_PATH, "tests", "fixtures", "C.jpg")

# Output files (ensure they do not exist)
outFile = os.path.join(PROJECT_PATH, "target", "python_out.jpg")
if os.path.exists(outFile):
    os.remove(outFile)
thumb_file = os.path.join(PROJECT_PATH, "target", "thumb_from_python.jpg")
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
        "name": "python_trusted_signer",
        "version": "0.0.1",
    }],
    "format": "image/jpeg",
    "title": "Python Test Image",
    "ingredients": [],
    "assertions": [
        {'label': 'stds.schema-org.CreativeWork',
            'data': {
                '@context': 'http://schema.org/',
                '@type': 'CreativeWork',
                'author': [
                    {'@type': 'Person',
                        'name': 'Prakash Duggaraju'
                     }
                ]
            },
            'kind': 'Json'
         }
    ]
}

# Create a trusted signer

# Example of signing a manifest store into a file
try:
    settings = c2pa_api.c2pa.ManifestBuilderSettings(generator="python-generator", settings=r"""
    {
        "trust" : {
            "trust_config": "1.3.6.1.5.5.7.3.36\n1.3.6.1.4.1.311.76.59.1.9"
        }
    }
    """)
    signer = TrustedSigner.default()
    c2pa_api.ManifestBuilder.sign_with_files(
        settings, signer.signer, manifestDefinition, testFile, outFile)
    # builder = c2pa_api.ManifestBuilder(settings, signer, manifestDefinition)
    # c2pa_api.ManifestBuilder.sign(testFile, outFile)

except Exception as e:
    print("Failed to sign manifest store: " + str(e), traceback.format_exc())
    exit(1)

print("manifest store written to " + outFile)
print(c2pa_api.ManifestStoreReader.from_file(outFile).read())
