// Validates the client's GraphQL operations against the Anvil schema in
// schema/anvil_schema.graphql (the copy published in @anvilco/types, see
// dev/fetch-schema.sh), so a query or mutation that no longer matches the API
// fails the build rather than a request.
//
// The operations are read from the client itself: dev/DumpOperations is a
// console app that references the Anvil project and prints every operation as
// JSON, so what is validated is exactly what the client sends.
//
// Usage: cd dev && npm ci && npm run check:graphql
import { execFileSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { buildSchema, parse, validate } from 'graphql'

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')

const schema = buildSchema(readFileSync(path.join(root, 'schema', 'anvil_schema.graphql'), 'utf8'))

const dump = execFileSync('dotnet', ['run', '--project', path.join(root, 'dev', 'DumpOperations')], {
  cwd: root,
  encoding: 'utf8',
  stdio: ['ignore', 'pipe', 'inherit'],
})
const operations = JSON.parse(dump)

let failed = false
for (const [name, document] of Object.entries(operations)) {
  let errors
  try {
    errors = validate(schema, parse(document))
  } catch (error) {
    errors = [error]
  }
  if (errors.length) {
    failed = true
    console.error(`✗ ${name}`)
    for (const error of errors) console.error(`    ${error.message}`)
  } else {
    console.log(`✓ ${name}`)
  }
}

if (failed) {
  console.error('\nSome operations do not match the Anvil schema in schema/anvil_schema.graphql.')
  process.exit(1)
}
