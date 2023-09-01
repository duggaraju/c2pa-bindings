// Copyright 2023 Adobe. All rights reserved.
// This file is licensed to you under the Apache License,
// Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
// or the MIT license (http://opensource.org/licenses/MIT),
// at your option.

// Unless required by applicable law or agreed to in writing,
// this software is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR REPRESENTATIONS OF ANY KIND, either express or
// implied. See the LICENSE-MIT and LICENSE-APACHE files for the
// specific language governing permissions and limitations under
// each license.

use crate::{
    error::{C2paError, Result},
    StreamError,
};
use c2pa::ManifestStore;
use std::io::{Read, Seek, Write};
use std::sync::RwLock;

struct ReaderSettings {}

/// The ManifestStoreReader reads the manifest store from a stream and then
/// provides access to the store via the json() and resource() methods.
///  
pub struct ManifestStoreReader {
    _settings: ReaderSettings,
    store: RwLock<ManifestStore>,
}

impl ManifestStoreReader {
    pub fn new() -> Self {
        Self {
            _settings: ReaderSettings {},
            store: RwLock::new(ManifestStore::new()),
        }
    }

    /// Reads the manifest store from a stream
    /// # Arguments
    /// * `format` - the format of the manifest store
    /// * `stream` - the stream to read from
    /// # Returns
    /// * `Result<String>` - the json representation of the manifest store
    ///    or an error
    /// 
    /// # Example
    /// ```
    /// use c2pa::ManifestStore;
    /// use c2pa::ManifestStoreReader; 
    /// use std::io::Cursor;
    ///     
    //
    pub fn read(&self, format: &str, mut stream: impl Read + Seek) -> Result<String> {
        // todo: use ManifestStore::from_stream, when it exists
        let mut bytes = Vec::new();
        let _len = stream.read_to_end(&mut bytes).map_err(|e| {
            C2paError::Stream(StreamError::Other {
                reason: e.to_string(),
            })
        })?;
        let store = ManifestStore::from_bytes(format, &bytes, true).map_err(C2paError::Sdk)?;
        let json = store.to_string();
        if let Ok(mut st) = self.store.try_write() {
            *st = store;
        } else {
            return Err(C2paError::RwLock);
        };
        Ok(json)
    }

    /// returns a json representation of the manifest store
    /// # Returns
    /// * `Result<String>` - the json representation of the manifest store
    ///     or an error
    /// 
    pub fn json(&self) -> Result<String> {
        self.store
            .try_read()
            .map(|store| (*store).to_string())
            .map_err(|e| C2paError::RwLock)
    }

    /// returns a resource from the manifest store
    /// # Arguments
    /// * `manifest` - the manifest id
    /// * `id` - the resource id
    /// # Returns
    /// * `Option<Vec<u8>>` - the resource bytes
    /// 
    pub fn resource(&self, manifest: &str, id: &str) -> Result<Vec<u8>> {
        if let Ok(store) = self.store.try_read() {
            match store.manifests().get(manifest) {
                Some(manifest) => {
                    match manifest.resources().get(id) {
                        Ok(r) => Ok(r.into_owned()),
                        Err(e) => {
                            Err(C2paError::Sdk(e))
                        }
                    }
                },
                None => {
                    Err(C2paError::Sdk(c2pa::Error::ResourceNotFound(manifest.to_string())))
                }
            }
        } else {
            return Err(C2paError::RwLock);
        }
    }

    /// writes a resource from the manifest store to the stream
    /// # Arguments
    /// * `manifest` - the manifest id
    /// * `id` - the resource id
    /// * `stream` - the stream to write to
    /// # Returns
    /// * `Result<()>` - Ok or an error
    /// 
    pub fn resource_write(&self, manifest_label: &str, id: &str, mut stream: impl Write + Seek) -> Result<()> {
        self.resource(manifest_label, id).and_then(|bytes| stream.write_all(&bytes).map_err(|e| {
            C2paError::Stream(StreamError::Other {
                reason: e.to_string(),
            })
        }))
    }
}