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
  isAdmin: boolean;
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

export interface RecipeIngredient {
  id: string;
  recipeId: string;
  name: string;
  quantity: number;
  unit: MeasurementUnit;
  notes?: string | null;
}

export interface Recipe {
  id: string;
  householdId: string;
  name: string;
  description?: string | null;
  servings: number;
  prepTimeMinutes: number;
  cookTimeMinutes: number;
  ingredients: RecipeIngredient[];
  instructions: string[];
  notes?: string | null;
  photos: string[];
  createdAt: string;
  updatedAt: string;
}

export interface CreateRecipeIngredient {
  name: string;
  quantity: number;
  unit: MeasurementUnit;
  notes?: string;
}

export interface CreateRecipe {
  name: string;
  description?: string;
  servings: number;
  prepTimeMinutes: number;
  cookTimeMinutes: number;
  ingredients: CreateRecipeIngredient[];
  instructions: string[];
  notes?: string;
}

export type UpdateRecipe = Partial<CreateRecipe>;

export interface MealPlan {
  id: string;
  householdId: string;
  recipeId: string;
  recipe?: Recipe | null;
  plannedDate: string; // DateTime from backend will be a string like "2025-01-15T00:00:00"
  mealName?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateMealPlan {
  recipeId: string;
  plannedDate: string; // Format: ISO 8601 DateTime string
  mealName?: string;
  notes?: string;
}

export type UpdateMealPlan = Partial<CreateMealPlan>;
