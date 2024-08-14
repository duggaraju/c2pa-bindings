use std::{io::{Cursor, Seek}, sync::RwLock};

use c2pa::{Builder, CAIRead, Signer, CAIReadWrite,};

use crate::{
    Result, C2paError, C2paSigner, StreamAdapter, Stream
};

pub struct ManifestBuilder {
    pub builder: RwLock<Builder>
}

impl ManifestBuilder {
    
    pub fn from_json( json: &str) -> Result<ManifestBuilder> {
        let builder_result = c2pa::Builder::from_json(json).map_err(C2paError::from)?;
        let locked_builder = RwLock::new(builder_result);
        let builder = ManifestBuilder { builder: locked_builder };
        Ok(builder)
    }

    pub fn add_ingredient<T>(&self, ingredient_json: T, format: &str, mut stream: &mut dyn CAIRead) -> Result<&Self> where T: Into<String> {
        
        let _ = self.builder.write().map_err(|_|C2paError::RwLock)?.add_ingredient(ingredient_json, format, &mut stream);
        Ok(self)
    }

    pub fn add_resource(&self, resource_id: &str, mut stream: &mut dyn CAIRead) -> Result<&Self> {

        let _ = self.builder.write().map_err(|_|C2paError::RwLock)?.add_resource(&resource_id, &mut stream);
        Ok(self)
    }

    pub fn set_format(&self, format: &str) -> Result<&Self> {
        let _ = self.builder.write().map_err(|_|C2paError::RwLock)?.set_format(format);
        Ok(self)
    }

    pub fn set_thumbnail(&self, format: &str, mut stream: &mut dyn CAIRead) -> Result<&Self> {
        let _ = self.builder.write().map_err(|_|C2paError::RwLock)?.set_thumbnail(format, &mut stream);
        Ok(self)
    }

    pub fn add_assertion(&self, label: &str, data: &str) -> Result<&Self> {
        let _ = self.builder.write().map_err(|_|C2paError::RwLock)?.add_assertion_json(label, &data);
        Ok(self)
    }

    pub fn sign_stream(&self, signer: &C2paSigner, input_mut: &dyn Stream, output_mut: &dyn Stream, ) -> Result<Vec<u8>> {
        let mut input = StreamAdapter::from(input_mut);
        let mut output = StreamAdapter::from(output_mut);
        self.sign(signer, &mut input, &mut output)
    }

    pub fn sign(&self, signer: &dyn Signer, input: &mut dyn CAIRead, output: &mut dyn CAIReadWrite) -> Result<Vec<u8>> {
        let format = self.builder.read().unwrap().definition.format.clone();

        let mut vec_source = Vec::new();

        input.read_to_end(&mut vec_source).map_err(C2paError::from)?;

        let mut source = Cursor::new(vec_source);
        let mut dest = Cursor::new(Vec::new());

        let result = self.builder.write().map_err(|_|C2paError::RwLock)?.sign(signer, &format, &mut source, &mut dest).map_err(C2paError::from)?;
        dest.rewind()?;
        
        output.write_all(&dest.into_inner()).map_err(C2paError::from)?;
        Ok(result.to_vec())
    }
}