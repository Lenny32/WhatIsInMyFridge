import type { CreateFoodItem, FoodItem, StorageLocation, UpdateFoodItem, LoginRequest, RegisterRequest, AuthResponse, User, Household, Recipe, CreateRecipe, UpdateRecipe } from "./types";
import { getToken } from "./auth";
import createClient from "openapi-fetch";
import type { paths } from "./api-types";

const API_BASE = import.meta.env.VITE_API_BASE || "__VITE_API_BASE__" || "";

/**
 * Debug error information attached to errors when the backend is running in development mode.
 * This includes full exception details including stack traces from the .NET backend.
 */
export interface DebugErrorInfo {
  type: string;
  message: string;
  stackTrace?: string;
  source?: string;
  innerException?: DebugErrorInfo;
}

// Type-safe API client for new endpoints
export const apiClient = createClient<paths>({ baseUrl: API_BASE });

apiClient.use({
  onRequest({ request }) {
    const token = getToken();
    if (token) {
      request.headers.set("Authorization", `Bearer ${token}`);
    }
    return request;
  },
});

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
    let debugInfo: any = null;
    
    try {
      const text = await response.text();
      if (text) {
        // Try to parse as JSON first
        try {
          const json = JSON.parse(text);
          
          // Check if this is a debug exception response
          if (json.type && json.message && json.stackTrace) {
            debugInfo = json;
            message = json.message;
          } else {
            message = json.error || json.title || text;
          }
        } catch {
          message = text;
        }
      } else {
        message = `Request failed with status ${response.status}`;
      }
    } catch {
      message = `Request failed with status ${response.status}`;
    }
    
    const error: any = new Error(message);
    if (debugInfo) {
      error.debugInfo = debugInfo;
      
      // Always log detailed exception to console when debug info is available
      console.group('%c🐛 API Exception Details', 'color: #ff6b6b; font-weight: bold; font-size: 14px;');
      console.error('%cType:', 'font-weight: bold;', debugInfo.type);
      console.error('%cMessage:', 'font-weight: bold;', debugInfo.message);
      if (debugInfo.source) {
        console.error('%cSource:', 'font-weight: bold;', debugInfo.source);
      }
      if (debugInfo.stackTrace) {
        console.error('%cStack Trace:', 'font-weight: bold;');
        console.error(debugInfo.stackTrace);
      }
      if (debugInfo.innerException) {
        console.group('%cInner Exception:', 'font-weight: bold; color: #ffa94d;');
        console.error('%cType:', 'font-weight: bold;', debugInfo.innerException.type);
        console.error('%cMessage:', 'font-weight: bold;', debugInfo.innerException.message);
        if (debugInfo.innerException.stackTrace) {
          console.error('%cStack Trace:', 'font-weight: bold;');
          console.error(debugInfo.innerException.stackTrace);
        }
        console.groupEnd();
      }
      console.groupEnd();
    }
    
    throw error;
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

// Recipes
export async function listRecipes(): Promise<Recipe[]> {
  return await request<Recipe[]>("/api/recipes");
}

export async function getRecipe(id: string): Promise<Recipe> {
  return await request<Recipe>(`/api/recipes/${id}`);
}

export async function createRecipe(payload: CreateRecipe): Promise<Recipe> {
  return await request<Recipe>("/api/recipes", {
    method: "POST",
    body: JSON.stringify(payload)
  });
}

export async function updateRecipe(id: string, payload: UpdateRecipe): Promise<Recipe> {
  return await request<Recipe>(`/api/recipes/${id}`, {
    method: "PATCH",
    body: JSON.stringify(payload)
  });
}

export async function deleteRecipe(id: string): Promise<void> {
  await request<void>(`/api/recipes/${id}`, {
    method: "DELETE"
  });
}

export async function getIngredientSuggestions(query: string): Promise<string[]> {
  return await request<string[]>(`/api/ingredients/suggestions?query=${encodeURIComponent(query)}`);
}

// Photo management
export async function uploadRecipePhoto(recipeId: string, file: File): Promise<{ photoId: string; url: string }> {
  const token = getToken();
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${API_BASE}/api/recipes/${recipeId}/photos`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${token}`,
    },
    body: formData,
  });

  if (!response.ok) {
    let message = "Failed to upload photo";
    let debugInfo: any = null;
    
    try {
      const json = await response.json();
      
      // Check if this is a debug exception response
      if (json.type && json.message && json.stackTrace) {
        debugInfo = json;
        message = json.message;
      } else {
        message = json.error || json.title || message;
      }
    } catch {
      message = `Upload failed with status ${response.status}`;
    }
    
    const error: any = new Error(message);
    if (debugInfo) {
      error.debugInfo = debugInfo;
      
      // Log detailed exception to console when debug info is available
      console.group('%c🐛 Photo Upload Exception Details', 'color: #ff6b6b; font-weight: bold; font-size: 14px;');
      console.error('%cType:', 'font-weight: bold;', debugInfo.type);
      console.error('%cMessage:', 'font-weight: bold;', debugInfo.message);
      if (debugInfo.source) {
        console.error('%cSource:', 'font-weight: bold;', debugInfo.source);
      }
      if (debugInfo.stackTrace) {
        console.error('%cStack Trace:', 'font-weight: bold;');
        console.error(debugInfo.stackTrace);
      }
      if (debugInfo.innerException) {
        console.group('%cInner Exception:', 'font-weight: bold; color: #ffa94d;');
        console.error('%cType:', 'font-weight: bold;', debugInfo.innerException.type);
        console.error('%cMessage:', 'font-weight: bold;', debugInfo.innerException.message);
        if (debugInfo.innerException.stackTrace) {
          console.error('%cStack Trace:', 'font-weight: bold;');
          console.error(debugInfo.innerException.stackTrace);
        }
        console.groupEnd();
      }
      console.groupEnd();
    }
    
    throw error;
  }

  return await response.json();
}

export async function deleteRecipePhoto(recipeId: string, photoId: string): Promise<void> {
  await request<void>(`/api/recipes/${recipeId}/photos/${photoId}`, {
    method: "DELETE",
  });
}

export function getPhotoUrl(photoId: string, extension: string = ".jpg"): string {
  return `${API_BASE}/api/photos/${photoId}${extension}`;
}
