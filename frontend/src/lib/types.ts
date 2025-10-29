export type StorageLocation = "fridge" | "freezer" | "pantry";

export interface FoodItem {
  id: string;
  name: string;
  location: StorageLocation;
  quantity: number;
  unit: string;
  restockThreshold: number;
  expiresAt?: string;
  category?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateFoodItem {
  name: string;
  location: StorageLocation;
  quantity: number;
  unit: string;
  restockThreshold: number;
  expiresAt?: string;
  category?: string;
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
