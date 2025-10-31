// Auto-generated stub for CI builds
// Run 'deno task generate-api' locally to generate from OpenAPI spec
export interface paths {
  "/api/grocery": {
    get: {
      responses: {
        200: {
          content: {
            "application/json": any[];
          };
        };
      };
    };
    post: {
      requestBody: {
        content: {
          "application/json": any;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": any;
          };
        };
      };
    };
  };
  "/api/grocery/{id}": {
    patch: {
      parameters: {
        path: {
          id: string;
        };
      };
      requestBody: {
        content: {
          "application/json": any;
        };
      };
      responses: {
        200: {
          content: {
            "application/json": any;
          };
        };
      };
    };
    delete: {
      parameters: {
        path: {
          id: string;
        };
      };
      responses: {
        204: never;
      };
    };
  };
  "/api/grocery/purchased": {
    delete: {
      responses: {
        204: never;
      };
    };
  };
}
export interface components {
  schemas: {
    FoodCategory: "Dairy" | "Cheese" | "Yogurt" | "Eggs" | "Meat" | "Poultry" | "Fish" | "Seafood" | "Vegetables" | "Fruits" | "Herbs" | "Bread" | "Pasta" | "Rice" | "Cereal" | "CannedGoods" | "Condiments" | "Sauces" | "Spices" | "Oils" | "Beverages" | "Juice" | "Soda" | "FrozenMeals" | "FrozenVegetables" | "FrozenFruits" | "IceCream" | "Snacks" | "Desserts" | "Candy" | "Leftovers" | "PreparedMeals" | "Other" | "Undefined";
    MeasurementUnit: "Pieces" | "Items" | "Grams" | "Kilograms" | "Ounces" | "Pounds" | "Milliliters" | "Liters" | "FluidOunces" | "Cups" | "Pints" | "Quarts" | "Gallons" | "Teaspoons" | "Tablespoons" | "Cans" | "Bottles" | "Jars" | "Boxes" | "Bags" | "Packages" | "Cartons" | "Undefined";
  };
}
