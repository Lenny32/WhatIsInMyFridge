/**
 * Type-safe API Client Example
 * 
 * This file demonstrates how to use the generated OpenAPI client after running `deno task generate-api`.
 * 
 * The actual implementation will depend on importing openapi-fetch which is configured in deno.json.
 * Since Vite handles the bundling, you can import directly during development.
 * 
 * Example usage:
 * 
 * ```typescript
 * import createClient from "openapi-fetch";
 * import type { paths } from "./api-types.ts";
 * import { getToken } from "./auth.ts";
 * 
 * const client = createClient<paths>({ baseUrl: "" });
 * 
 * client.use({
 *   onRequest({ request }) {
 *     const token = getToken();
 *     if (token) {
 *       request.headers.set("Authorization", `Bearer ${token}`);
 *     }
 *     return request;
 *   },
 * });
 * 
 * // Now use the client with full type safety:
 * const { data, error } = await client.GET("/api/items");
 * if (data) {
 *   // data is fully typed!
 *   data.forEach(item => {
 *     console.log(item.name, item.quantity);
 *   });
 * }
 * 
 * const { data: newItem } = await client.POST("/api/items", {
 *   body: {
 *     name: "Milk",
 *     quantity: 1,
 *     location: "fridge",
 *     toBuy: false
 *   }
 * });
 * ```
 */

export {};
