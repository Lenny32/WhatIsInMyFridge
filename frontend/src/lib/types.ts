import type { components } from "./api-types.ts";

export type StorageLocation = "Fridge" | "Freezer" | "Pantry";
export type FoodCategory = components["schemas"]["FoodCategory"];
export type MeasurementUnit = components["schemas"]["MeasurementUnit"];

export interface FoodItem {
  id: string;
  name: string;
  location: StorageLocation;
  quantity: number;
  unit: MeasurementUnit;
  restockThreshold: number;
  expiresAt?: string;
  category?: FoodCategory | null;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateFoodItem {
  name: string;
  location: StorageLocation;
  quantity: number;
  unit: MeasurementUnit;
  restockThreshold: number;
  expiresAt?: string;
  category?: FoodCategory;
  notes?: string;
}

export type UpdateFoodItem = Partial<CreateFoodItem>;

export interface User {
  id: string;
  email: string;
  name: string;
  householdIds: string[];
  currentHouseholdId?: string | null;
}

export interface Household {
  id: string;
  name: string;
  ownerId: string;
  memberIds: string[];
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  name: string;
  householdName: string;
}

export interface AuthResponse {
  token: string;
  user: User;
  household?: Household;
}
