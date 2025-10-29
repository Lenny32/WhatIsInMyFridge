#!/usr/bin/env -S deno run -A

/**
 * OpenAPI Client Generator
 * 
 * Fetches the OpenAPI spec from the running backend and generates TypeScript types.
 * 
 * Usage:
 *   deno task generate-api
 *   deno task generate-api http://localhost:5000/swagger/v1/swagger.json
 */

const API_URL = Deno.args[0] || "http://localhost:5000/swagger/v1/swagger.json";
const OUTPUT_PATH = new URL("../src/lib/api-types.ts", import.meta.url).pathname;

console.log("🔍 Fetching OpenAPI spec from:", API_URL);

try {
  const response = await fetch(API_URL);
  
  if (!response.ok) {
    throw new Error(`Failed to fetch OpenAPI spec: ${response.status} ${response.statusText}`);
  }

  const spec = await response.json();
  console.log("✅ OpenAPI spec fetched successfully");

  // Save the spec temporarily
  const tempSpecPath = new URL("../openapi-spec.json", import.meta.url).pathname;
  await Deno.writeTextFile(tempSpecPath, JSON.stringify(spec, null, 2));
  
  console.log("🔨 Generating TypeScript types...");
  
  // Run openapi-typescript CLI
  const command = new Deno.Command("deno", {
    args: [
      "run",
      "-A",
      "npm:openapi-typescript@7",
      tempSpecPath,
      "--output",
      OUTPUT_PATH
    ],
    stdout: "inherit",
    stderr: "inherit",
  });

  const { code } = await command.output();
  
  // Clean up temp file
  await Deno.remove(tempSpecPath);

  if (code !== 0) {
    throw new Error(`openapi-typescript exited with code ${code}`);
  }

  console.log("✅ Types generated successfully at:", OUTPUT_PATH);
  console.log("\n💡 You can now use the typed API client in your code!");
  
} catch (error) {
  console.error("❌ Error generating API types:");
  
  if (error instanceof Error && error.message.includes("Failed to fetch")) {
    console.error("\n⚠️  Make sure the backend is running:");
    console.error("   cd backend && dotnet run --project WhatIsInMyFridge.Api\n");
  }
  
  console.error(error);
  Deno.exit(1);
}
