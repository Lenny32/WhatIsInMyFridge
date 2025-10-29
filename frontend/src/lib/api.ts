import type { CreateFoodItem, FoodItem, StorageLocation, UpdateFoodItem, LoginRequest, RegisterRequest, AuthResponse, User, Household } from "./types.ts";
import { getToken } from "./auth.ts";

const API_BASE = "";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json"
  };
  
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  const response = await fetch(`${API_BASE}${path}`, {
    headers,
    ...init
  });

  if (!response.ok) {
    let message = "Request failed";
    try {
      const text = await response.text();
      if (text) {
        // Try to parse as JSON first
        try {
          const json = JSON.parse(text);
          message = json.error || json.title || text;
        } catch {
          message = text;
        }
      } else {
        message = `Request failed with status ${response.status}`;
      }
    } catch {
      message = `Request failed with status ${response.status}`;
    }
    throw new Error(message);
  }

  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T;
  }

  // Check if response has content
  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('application/json')) {
    return await response.json() as T;
  }
  
  // If no JSON content, return undefined
  return undefined as T;
}

// Authentication
export async function login(payload: LoginRequest): Promise<AuthResponse> {
  return await request<AuthResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export async function register(payload: RegisterRequest): Promise<AuthResponse> {
  return await request<AuthResponse>("/api/auth/register", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export async function logout(): Promise<void> {
  await request<void>("/api/auth/logout", {
    method: "POST"
  });
}

export async function getCurrentUser(): Promise<{ user: User; household?: Household }> {
  return await request<{ user: User; household?: Household }>("/api/auth/me");
}

// Food Items
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
