use std::io::Cursor;

use c2pa::{Builder, CAIRead, Signer, CAIReadWrite,};

use crate::{
    Result, C2paError, C2paSigner, StreamAdapter, Stream
};


pub struct ManifestBuilderSettings {
    pub generator: String,
    pub settings: String,
}

pub struct ManifestBuilder {
    pub builder: Builder,
}

impl ManifestBuilder {
    pub fn add_ingredient<T>(&mut self, ingredient_json: T, format: &str, mut stream: &mut dyn CAIRead) -> Result<&Self> where T: Into<String> {
        
        self.builder.add_ingredient(ingredient_json, format, &mut stream);
        Ok(self)
    }

    pub fn add_resource(&mut self, resource_id: &str, mut stream: &mut dyn CAIRead) -> Result<&Self> {

        self.builder.add_resource(&resource_id, &mut stream);
        Ok(self)
    }

    pub fn from_json(&mut self, json: &str) -> Result<()> {
        self.builder = c2pa::Builder::from_json(json).map_err(C2paError::from)?;
        Ok(())
    }

    pub fn sign_stream(&mut self, signer: &C2paSigner, input_mut: &dyn Stream, output_mut: &dyn Stream, ) -> Result<Vec<u8>> {
        let mut input = StreamAdapter::from(input_mut);
        let mut output = StreamAdapter::from(output_mut);
        self.sign(signer, &mut input, &mut output)
    }

    pub fn sign(&mut self, signer: &dyn Signer, input: &mut dyn CAIRead, output: &mut dyn CAIReadWrite) -> Result<Vec<u8>> {
        let format = self.builder.definition.format.clone();

        let mut vec_source = Vec::new();
        let mut vec_dest = Vec::new();

        input.read_to_end(&mut vec_source).map_err(C2paError::from)?;
        output.read_to_end(&mut vec_dest).map_err(C2paError::from)?;

        let mut source = Cursor::new(vec_source);
        let mut dest = Cursor::new(vec_dest);

        let result = self.builder.sign(signer, &format, &mut source, &mut dest).map_err(C2paError::from)?;
        return Ok(result.to_vec());
    }
}