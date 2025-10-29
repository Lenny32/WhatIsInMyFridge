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
  trackShoppingList: boolean;
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
  trackShoppingList?: boolean;
  notes?: string;
}

export type UpdateFoodItem = Partial<CreateFoodItem>;
