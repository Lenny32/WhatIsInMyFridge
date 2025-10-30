<script lang="ts">
  import { onMount } from 'svelte';
  import { apiClient } from './api';
  import type { components } from './api-types';

  type FoodCategory = components['schemas']['FoodCategory'];

  interface GroceryItem {
    id: string;
    householdId: string;
    name: string;
    quantity?: number | null;
    category: FoodCategory;
    notes?: string | null;
    isPurchased: boolean;
    createdAt: string;
    updatedAt: string;
  }

  let groceryItems: GroceryItem[] = [];
  let newItemName = '';
  let newItemQuantity = '';
  let newItemCategory: FoodCategory = 'Other';
  let newItemNotes = '';
  let loading = false;
  let error = '';

  const categories: FoodCategory[] = [
    'Fruits',
    'Vegetables',
    'Meat',
    'Dairy',
    'Bread',
    'Condiments',
    'Beverages',
    'Snacks',
    'FrozenMeals',
    'Other'
  ];

  onMount(() => {
    loadGroceryItems();
  });

  async function loadGroceryItems() {
    loading = true;
    error = '';
    try {
      const { data, error: apiError } = await apiClient.GET('/api/grocery');
      if (apiError) {
        error = 'Failed to load grocery list';
        console.error(apiError);
      } else {
        groceryItems = (data as any) || [];
      }
    } catch (err) {
      error = 'Failed to load grocery list';
      console.error(err);
    } finally {
      loading = false;
    }
  }

  async function addItem() {
    if (!newItemName.trim()) return;

    loading = true;
    error = '';
    try {
      const quantityValue = newItemQuantity.trim() ? parseFloat(newItemQuantity.trim()) : null;
      const { data, error: apiError } = await apiClient.POST('/api/grocery', {
        body: {
          name: newItemName.trim(),
          quantity: quantityValue,
          category: newItemCategory,
          notes: newItemNotes.trim() || null
        }
      });

      if (apiError) {
        error = 'Failed to add item';
        console.error(apiError);
      } else if (data) {
        groceryItems = [...groceryItems, data as any];
        // Reset form
        newItemName = '';
        newItemQuantity = '';
        newItemCategory = 'Other';
        newItemNotes = '';
      }
    } catch (err) {
      error = 'Failed to add item';
      console.error(err);
    } finally {
      loading = false;
    }
  }

  async function togglePurchased(item: GroceryItem) {
    loading = true;
    error = '';
    try {
      const { data, error: apiError } = await apiClient.PATCH('/api/grocery/{id}', {
        params: { path: { id: item.id } },
        body: { isPurchased: !item.isPurchased }
      });

      if (apiError) {
        error = 'Failed to update item';
        console.error(apiError);
      } else if (data) {
        groceryItems = groceryItems.map(i => i.id === item.id ? (data as any) : i);
      }
    } catch (err) {
      error = 'Failed to update item';
      console.error(err);
    } finally {
      loading = false;
    }
  }

  async function deleteItem(id: string) {
    loading = true;
    error = '';
    try {
      const { error: apiError } = await apiClient.DELETE('/api/grocery/{id}', {
        params: { path: { id } }
      });

      if (apiError) {
        error = 'Failed to delete item';
        console.error(apiError);
      } else {
        groceryItems = groceryItems.filter(i => i.id !== id);
      }
    } catch (err) {
      error = 'Failed to delete item';
      console.error(err);
    } finally {
      loading = false;
    }
  }

  async function clearPurchased() {
    if (!confirm('Remove all purchased items?')) return;

    loading = true;
    error = '';
    try {
      const { error: apiError } = await apiClient.DELETE('/api/grocery/purchased');

      if (apiError) {
        error = 'Failed to clear purchased items';
        console.error(apiError);
      } else {
        groceryItems = groceryItems.filter(i => !i.isPurchased);
      }
    } catch (err) {
      error = 'Failed to clear purchased items';
      console.error(err);
    } finally {
      loading = false;
    }
  }

  function formatQuantity(item: GroceryItem): string {
    return item.quantity ? String(item.quantity) : '';
  }

  $: unpurchasedCount = groceryItems.filter(i => !i.isPurchased).length;
  $: purchasedCount = groceryItems.filter(i => i.isPurchased).length;
</script>

<div class="grocery-list">
  <h2>Grocery List</h2>

  {#if error}
    <div class="error">{error}</div>
  {/if}

  <div class="add-item-form">
    <h3>Add Item</h3>
    <form on:submit|preventDefault={addItem}>
      <div class="form-row">
        <input
          type="text"
          placeholder="Item name"
          bind:value={newItemName}
          disabled={loading}
          required
        />
        <input
          type="number"
          step="any"
          placeholder="Quantity (optional)"
          bind:value={newItemQuantity}
          disabled={loading}
          style="max-width: 200px;"
        />
      </div>
      <div class="form-row">
        <select bind:value={newItemCategory} disabled={loading}>
          {#each categories as category}
            <option value={category}>{category}</option>
          {/each}
        </select>
        <input
          type="text"
          placeholder="Notes (optional)"
          bind:value={newItemNotes}
          disabled={loading}
        />
      </div>
      <button type="submit" disabled={loading || !newItemName.trim()}>
        Add to List
      </button>
    </form>
  </div>

  <div class="summary">
    <span>{unpurchasedCount} items to buy</span>
    {#if purchasedCount > 0}
      <button class="clear-btn" on:click={clearPurchased} disabled={loading}>
        Clear {purchasedCount} purchased
      </button>
    {/if}
  </div>

  {#if loading && groceryItems.length === 0}
    <p>Loading...</p>
  {:else if groceryItems.length === 0}
    <p class="empty">Your grocery list is empty. Add items above!</p>
  {:else}
    <ul class="items-list">
      {#each groceryItems as item (item.id)}
        <li class:purchased={item.isPurchased}>
          <input
            type="checkbox"
            checked={item.isPurchased}
            on:change={() => togglePurchased(item)}
            disabled={loading}
          />
          <div class="item-details">
            <div class="item-header">
              <strong>{item.name}</strong>
              {#if formatQuantity(item)}
                <span class="quantity">{formatQuantity(item)}</span>
              {/if}
            </div>
            <div class="item-meta">
              <span class="category">{item.category}</span>
              {#if item.notes}
                <span class="notes">{item.notes}</span>
              {/if}
            </div>
          </div>
          <button
            class="delete-btn"
            on:click={() => deleteItem(item.id)}
            disabled={loading}
            title="Delete item"
          >
            ✕
          </button>
        </li>
      {/each}
    </ul>
  {/if}
</div>

<style>
  .grocery-list {
    max-width: 800px;
    margin: 0 auto;
  }

  h2 {
    margin-bottom: 1.5rem;
  }

  h3 {
    margin-bottom: 0.75rem;
    font-size: 1.1rem;
  }

  .error {
    background-color: #fee;
    color: #c33;
    padding: 0.75rem;
    border-radius: 4px;
    margin-bottom: 1rem;
  }

  .add-item-form {
    background: #f8f9fa;
    padding: 1.5rem;
    border-radius: 8px;
    margin-bottom: 1.5rem;
  }

  .form-row {
    display: flex;
    gap: 0.5rem;
    margin-bottom: 0.75rem;
  }

  .form-row:last-of-type {
    margin-bottom: 1rem;
  }

  input, select {
    flex: 1;
    padding: 0.5rem;
    border: 1px solid #ddd;
    border-radius: 4px;
    font-size: 1rem;
  }

  button {
    padding: 0.5rem 1rem;
    background-color: #007bff;
    color: white;
    border: none;
    border-radius: 4px;
    cursor: pointer;
    font-size: 1rem;
  }

  button:hover:not(:disabled) {
    background-color: #0056b3;
  }

  button:disabled {
    background-color: #ccc;
    cursor: not-allowed;
  }

  .summary {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1rem;
    font-weight: 500;
  }

  .clear-btn {
    background-color: #6c757d;
    padding: 0.4rem 0.8rem;
    font-size: 0.9rem;
  }

  .clear-btn:hover:not(:disabled) {
    background-color: #545b62;
  }

  .empty {
    text-align: center;
    color: #666;
    padding: 2rem;
    background: #f8f9fa;
    border-radius: 8px;
  }

  .items-list {
    list-style: none;
    padding: 0;
    margin: 0;
  }

  .items-list li {
    display: flex;
    align-items: flex-start;
    gap: 0.75rem;
    padding: 1rem;
    border: 1px solid #ddd;
    border-radius: 8px;
    margin-bottom: 0.5rem;
    background: white;
    transition: all 0.2s;
  }

  .items-list li:hover {
    box-shadow: 0 2px 4px rgba(0,0,0,0.1);
  }

  .items-list li.purchased {
    opacity: 0.6;
    background: #f8f9fa;
  }

  .items-list li.purchased .item-details {
    text-decoration: line-through;
  }

  input[type="checkbox"] {
    margin-top: 0.2rem;
    width: 1.2rem;
    height: 1.2rem;
    flex-shrink: 0;
  }

  .item-details {
    flex: 1;
    min-width: 0;
  }

  .item-header {
    display: flex;
    justify-content: space-between;
    align-items: baseline;
    gap: 0.5rem;
    margin-bottom: 0.25rem;
  }

  .quantity {
    color: #666;
    font-size: 0.9rem;
    white-space: nowrap;
  }

  .item-meta {
    display: flex;
    gap: 0.75rem;
    font-size: 0.85rem;
    color: #666;
  }

  .category {
    text-transform: capitalize;
    background: #e9ecef;
    padding: 0.15rem 0.5rem;
    border-radius: 3px;
  }

  .notes {
    font-style: italic;
  }

  .delete-btn {
    background-color: #dc3545;
    color: white;
    border: none;
    border-radius: 4px;
    width: 2rem;
    height: 2rem;
    padding: 0;
    font-size: 1.2rem;
    line-height: 1;
    cursor: pointer;
    flex-shrink: 0;
  }

  .delete-btn:hover:not(:disabled) {
    background-color: #c82333;
  }
</style>
