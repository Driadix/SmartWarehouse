import fs from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import Ajv2020 from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const schemaDirectory = path.resolve(__dirname, '../../docs/api/schemas/southbound');

async function loadSchemas(directory) {
  const entries = await fs.readdir(directory, { withFileTypes: true });
  const schemaFiles = entries
    .filter((entry) => entry.isFile() && entry.name.endsWith('.json'))
    .map((entry) => path.join(directory, entry.name))
    .sort();

  return Promise.all(
    schemaFiles.map(async (filePath) => ({
      filePath,
      schema: JSON.parse(await fs.readFile(filePath, 'utf8'))
    }))
  );
}

try {
  const schemas = await loadSchemas(schemaDirectory);
  const ajv = new Ajv2020({
    strict: false,
    allErrors: true,
    validateFormats: true
  });

  addFormats(ajv);

  for (const { filePath, schema } of schemas) {
    ajv.addSchema(schema, schema.$id ?? filePath);
  }

  for (const { filePath, schema } of schemas) {
    ajv.getSchema(schema.$id ?? filePath) ?? ajv.compile(schema);
    console.log(`JSON Schema validation passed: ${path.basename(filePath)}`);
  }
} catch (error) {
  console.error('JSON Schema validation failed.');
  console.error(error.message);
  process.exit(1);
}
