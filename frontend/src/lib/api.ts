import type { CreateFoodItem, FoodItem, StorageLocation, UpdateFoodItem } from "./types.ts";

const API_BASE = (import.meta.env.VITE_API_BASE as string | undefined) ?? "";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    headers: {
      "Content-Type": "application/json"
    },
    ...init
  });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || "Request failed");
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return await response.json() as T;
}

export async function listItems(location?: StorageLocation): Promise<FoodItem[]> {
  const search = location ? `?location=${encodeURIComponent(location)}` : "";
  return await request<FoodItem[]>(`/api/items${search}`);
}

export async function getItem(id: string): Promise<FoodItem> {
  return await request<FoodItem>(`/api/items/${id}`);
}

export async function createItem(payload: CreateFoodItem): Promise<FoodItem> {
  return await request<FoodItem>("/api/items", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export async function updateItem(id: string, payload: UpdateFoodItem): Promise<FoodItem> {
  return await request<FoodItem>(`/api/items/${id}`, {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export async function deleteItem(id: string): Promise<void> {
  await request<void>(`/api/items/${id}`, {
    method: "DELETE"
  });
}

export async function listToBuy(): Promise<FoodItem[]> {
  return await request<FoodItem[]>("/api/items/to-buy");
}
