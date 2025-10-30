<script lang="ts">
  import { onMount } from "svelte";
  import type { FoodItem, StorageLocation } from "../lib/types";
  import type { components } from "../lib/api-types";
  import {
    createItem,
    deleteItem,
    listItems,
    listToBuy,
    updateItem,
    logout
  } from "../lib/api";
  import { authStore } from "../lib/auth";

  type FoodCategory = components["schemas"]["FoodCategory"];
  type MeasurementUnit = components["schemas"]["MeasurementUnit"];

  const locations: StorageLocation[] = ["Fridge", "Freezer", "Pantry"];

  const categories: FoodCategory[] = [
    "Dairy", "Cheese", "Yogurt", "Eggs",
    "Meat", "Poultry", "Fish", "Seafood",
    "Vegetables", "Fruits", "Herbs",
    "Bread", "Pasta", "Rice", "Cereal",
    "CannedGoods", "Condiments", "Sauces", "Spices", "Oils",
    "Beverages", "Juice", "Soda",
    "FrozenMeals", "FrozenVegetables", "FrozenFruits", "IceCream",
    "Snacks", "Desserts", "Candy",
    "Leftovers", "PreparedMeals", "Other",
    "Undefined"
  ];

  const units: MeasurementUnit[] = [
    "Pieces", "Items",
    "Grams", "Kilograms", "Ounces", "Pounds",
    "Milliliters", "Liters", "FluidOunces",
    "Cups", "Pints", "Quarts", "Gallons",
    "Teaspoons", "Tablespoons",
    "Cans", "Bottles", "Jars", "Boxes", "Bags", "Packages", "Cartons",
    "Undefined"
  ];

  let activeLocation: StorageLocation = "Fridge";
  let items: FoodItem[] = [];
  let shoppingList: FoodItem[] = [];
  let loading = false;
  let saving = false;
  let errorMessage: string | null = null;

  const emptyForm = () => ({
    name: "",
    location: activeLocation,
    quantity: 1,
    unit: "Pieces" as MeasurementUnit,
    restockThreshold: 1,
    expiresAt: "",
    category: "Undefined" as FoodCategory,
    notes: ""
  });

  let form = emptyForm();

  onMount(async () => {
    await reloadAll();
  });

  async function reloadAll() {
    await Promise.all([loadItems(activeLocation), loadShoppingList()]);
  }

  async function loadItems(location: StorageLocation) {
    loading = true;
    errorMessage = null;

    try {
      items = await listItems(location);
    } catch (error) {
      console.error(error);
      errorMessage = "Unable to load items.";
    } finally {
      loading = false;
    }
  }

  async function loadShoppingList() {
    try {
      shoppingList = await listToBuy();
    } catch (error) {
      console.error(error);
    }
  }

  function formatDate(value?: string | null) {
    if (!value) return "—";
    try {
      return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(value));
    } catch (error) {
      console.error("Failed to format date", error);
      return value;
    }
  }

  async function handleTabChange(location: StorageLocation) {
    activeLocation = location;
    form = { ...form, location };
    await loadItems(location);
  }

  async function handleSubmit() {
    saving = true;
    errorMessage = null;
    try {
      const payload = {
        ...form,
        quantity: Number(form.quantity),
        restockThreshold: Number(form.restockThreshold),
        expiresAt: form.expiresAt ? new Date(form.expiresAt).toISOString() : undefined,
        notes: form.notes?.trim() || undefined
      };
      await createItem(payload);
      form = emptyForm();
      await reloadAll();
    } catch (error) {
      console.error(error);
      errorMessage = "Unable to save item. Check the form values.";
    } finally {
      saving = false;
    }
  }

  async function adjustQuantity(item: FoodItem, delta: number) {
    const nextQuantity = Math.max(0, item.quantity + delta);
    if (nextQuantity === item.quantity) return;
    try {
      await updateItem(item.id, { quantity: nextQuantity });
      await reloadAll();
    } catch (error) {
      console.error(error);
      errorMessage = "Unable to update quantity.";
    }
  }

  async function removeItem(item: FoodItem) {
    if (!confirm(`Remove ${item.name}?`)) return;
    try {
      await deleteItem(item.id);
      await reloadAll();
    } catch (error) {
      console.error(error);
      errorMessage = "Unable to delete item.";
    }
  }

  function isLowStock(item: FoodItem) {
    return item.quantity <= item.restockThreshold;
  }

  function isExpired(item: FoodItem) {
    if (!item.expiresAt) return false;
    return new Date(item.expiresAt) < new Date();
  }

  async function handleLogout() {
    try {
      await logout();
      authStore.clear();
    } catch (error) {
      console.error("Logout failed:", error);
    }
  }

  $: userName = $authStore.user?.name || "User";
  $: householdName = $authStore.household?.name || "Household";
</script>

<main>
  <header>
    <div class="header-top">
      <div class="user-info">
        <div class="user-avatar">
          {userName.charAt(0).toUpperCase()}
        </div>
        <div class="user-details">
          <div class="user-name">{userName}</div>
          <div class="household-badge">{householdName}</div>
        </div>
      </div>
      <button type="button" class="logout-btn" on:click={handleLogout}>
        Sign Out
      </button>
    </div>
    <div class="header-content">
      <h1>Food Inventory Manager</h1>
      <p class="subtitle">
        Track pantry, freezer, and fridge items. Anything at or below its threshold
        flows into the shopping list automatically.
      </p>
    </div>
  </header>

  <div class="layout">
    <section class="inventory-card">
      <h2>Inventory</h2>
      <div class="tabs">
        {#each locations as location}
          <button
            type="button"
            class:active={activeLocation === location}
            on:click={() => handleTabChange(location)}
          >
            {location.charAt(0).toUpperCase() + location.slice(1)}
          </button>
        {/each}
      </div>

      {#if errorMessage}
        <div class="pill status-expired">{errorMessage}</div>
      {/if}

      {#if loading}
        <p>Loading inventory…</p>
      {:else if items.length === 0}
        <p>No items tracked here yet. Add your first entry below.</p>
      {:else}
        <table>
          <thead>
            <tr>
              <th>Name</th>
              <th>Qty</th>
              <th>Restock</th>
              <th>Expiry</th>
              <th>Notes</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {#each items as item}
              <tr>
                <td>
                  <div>
                    <strong>{item.name}</strong>
                    {#if item.category}
                      <div class="pill">{item.category}</div>
                    {/if}
                  </div>
                </td>
                <td>
                  <div class="inline-actions">
                    <button class="secondary" on:click={() => adjustQuantity(item, -1)}>-</button>
                    <span>{item.quantity} {item.unit}</span>
                    <button class="secondary" on:click={() => adjustQuantity(item, 1)}>+</button>
                  </div>
                </td>
                <td>
                  <span class:status-low={isLowStock(item)}>
                    {item.restockThreshold} {item.unit}
                  </span>
                </td>
                <td>
                  <span class:status-expired={isExpired(item)}>{formatDate(item.expiresAt)}</span>
                </td>
                <td>{item.notes ?? "—"}</td>
                <td>
                  <button class="danger" on:click={() => removeItem(item)}>Delete</button>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
        
        <div class="mobile-items-list">
          {#each items as item}
            <div class="mobile-item-card">
              <div class="mobile-item-header">
                <div class="mobile-item-title">
                  <strong>{item.name}</strong>
                  {#if item.category}
                    <div class="pill">{item.category}</div>
                  {/if}
                </div>
              </div>
              
              <div class="mobile-item-details">
                <div class="mobile-item-detail">
                  <span class="mobile-item-detail-label">Quantity</span>
                  <span>{item.quantity} {item.unit}</span>
                </div>
                <div class="mobile-item-detail">
                  <span class="mobile-item-detail-label">Restock At</span>
                  <span class:status-low={isLowStock(item)}>
                    {item.restockThreshold} {item.unit}
                  </span>
                </div>
                <div class="mobile-item-detail">
                  <span class="mobile-item-detail-label">Expiry</span>
                  <span class:status-expired={isExpired(item)}>{formatDate(item.expiresAt)}</span>
                </div>
                {#if item.notes}
                  <div class="mobile-item-detail">
                    <span class="mobile-item-detail-label">Notes</span>
                    <span>{item.notes}</span>
                  </div>
                {/if}
              </div>
              
              <div class="mobile-item-actions">
                <div class="inline-actions">
                  <button class="secondary" on:click={() => adjustQuantity(item, -1)}>-</button>
                  <span>{item.quantity} {item.unit}</span>
                  <button class="secondary" on:click={() => adjustQuantity(item, 1)}>+</button>
                </div>
                <button class="danger" on:click={() => removeItem(item)}>Delete</button>
              </div>
            </div>
          {/each}
        </div>
      {/if}
    </section>

    <aside class="inventory-card">
      <h2>Add Item</h2>
      <form on:submit|preventDefault={handleSubmit} class="form-grid">
        <div>
          <label for="name">Name</label>
          <input id="name" bind:value={form.name} required placeholder="Frozen berries" />
        </div>
        <div>
          <label for="location">Location</label>
          <select
            id="location"
            bind:value={form.location}
          >
            {#each locations as location}
              <option value={location}>{location}</option>
            {/each}
          </select>
        </div>
        <div>
          <label for="quantity">Quantity</label>
          <input
            id="quantity"
            type="number"
            min="0"
            bind:value={form.quantity}
            required
          />
        </div>
        <div>
          <label for="unit">Unit</label>
          <select id="unit" bind:value={form.unit} required>
            {#each units as unit}
              <option value={unit}>{unit}</option>
            {/each}
          </select>
        </div>
        <div>
          <label for="threshold">Restock threshold</label>
          <input
            id="threshold"
            type="number"
            min="0"
            bind:value={form.restockThreshold}
            required
          />
        </div>
        <div>
          <label for="expiry">Expiration date</label>
          <input id="expiry" type="date" bind:value={form.expiresAt} />
        </div>
        <div>
          <label for="category">Category</label>
          <select id="category" bind:value={form.category}>
            {#each categories as category}
              <option value={category}>{category}</option>
            {/each}
          </select>
        </div>
        <div>
          <label for="notes">Notes</label>
          <textarea id="notes" bind:value={form.notes} rows="2" placeholder="Brand or flavor details" />
        </div>
        <div class="form-actions">
          <button type="button" class="secondary" on:click={() => (form = emptyForm())}>
            Reset
          </button>
          <button type="submit" disabled={saving || !form.name.trim()}>
            {saving ? "Saving…" : "Add item"}
          </button>
        </div>
      </form>

      <h2>To Buy</h2>
      {#if shoppingList.length === 0}
        <p>Nothing to buy — you are fully stocked.</p>
      {:else}
        <ul>
          {#each shoppingList as item}
            <li>
              <strong>{item.name}</strong> — {item.quantity}/{item.restockThreshold} {item.unit}
              <span class="pill">{item.location}</span>
            </li>
          {/each}
        </ul>
      {/if}
    </aside>
  </div>
</main>

<style>
  header {
    margin-bottom: 2rem;
  }

  .header-top {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 1rem 1.25rem;
    background: var(--bg-header);
    border-radius: 12px;
    margin-bottom: 1.5rem;
    box-shadow: var(--shadow-header);
    border: 1px solid var(--border-secondary);
  }

  .user-info {
    display: flex;
    align-items: center;
    gap: 0.875rem;
  }

  .user-avatar {
    width: 42px;
    height: 42px;
    border-radius: 50%;
    background: linear-gradient(135deg, #2f80ed 0%, #1e5bb8 100%);
    display: flex;
    align-items: center;
    justify-content: center;
    font-weight: 600;
    font-size: 1.125rem;
    color: white;
    box-shadow: 0 2px 8px rgba(47, 128, 237, 0.3);
  }

  .user-details {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .user-name {
    font-weight: 600;
    font-size: 1rem;
    color: var(--text-primary);
  }

  .household-badge {
    font-size: 0.8rem;
    color: var(--text-tertiary);
    background: var(--bg-pill);
    padding: 0.125rem 0.5rem;
    border-radius: 4px;
    width: fit-content;
  }

  .logout-btn {
    background: var(--bg-button-secondary);
    border: 1px solid var(--border-primary);
    color: var(--text-button-secondary);
    padding: 0.5rem 1.25rem;
    border-radius: 6px;
    font-size: 0.9rem;
    font-weight: 500;
    cursor: pointer;
    transition: all 0.2s ease;
  }

  .logout-btn:hover {
    filter: brightness(1.1);
  }

  .header-content h1 {
    font-size: 1.75rem;
    margin-bottom: 0.5rem;
  }

  .subtitle {
    color: var(--text-tertiary);
    font-size: 0.95rem;
    line-height: 1.5;
    margin: 0;
  }
  
  @media screen and (max-width: 767px) {
    .header-top {
      flex-direction: row;
      gap: 1rem;
      align-items: center;
    }
    
    .user-info {
      flex: 1;
    }
    
    .logout-btn {
      padding: 0.4rem 1rem;
      font-size: 0.85rem;
    }
    
    .header-content h1 {
      font-size: 1.5rem;
    }
    
    .subtitle {
      font-size: 0.9rem;
    }
  }
</style>
