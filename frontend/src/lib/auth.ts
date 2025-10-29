import { writable } from "svelte/store";
import type { User, Household } from "./types.ts";

export interface AuthState {
  user: User | null;
  household: Household | null;
  isAuthenticated: boolean;
  isLoading: boolean;
}

// Token management
const TOKEN_KEY = "auth_token";

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

function createAuthStore() {
  const { subscribe, set, update } = writable<AuthState>({
    user: null,
    household: null,
    isAuthenticated: false,
    isLoading: true
  });

  return {
    subscribe,
    setUser: (user: User | null, household?: Household | null) => {
      update(state => ({
        ...state,
        user,
        household: household ?? null,
        isAuthenticated: !!user,
        isLoading: false
      }));
    },
    setLoading: (isLoading: boolean) => {
      update(state => ({ ...state, isLoading }));
    },
    clear: () => {
      clearToken();
      set({
        user: null,
        household: null,
        isAuthenticated: false,
        isLoading: false
      });
    }
  };
}

export const authStore = createAuthStore();
