<script lang="ts">
  import { onMount } from "svelte";
  import type { FoodItem, StorageLocation } from "./lib/types.ts";
  import {
    createItem,
    deleteItem,
    listItems,
    listToBuy,
    updateItem
  } from "./lib/api.ts";

  const locations: StorageLocation[] = ["fridge", "freezer", "pantry"];

  let activeLocation: StorageLocation = "fridge";
  let items: FoodItem[] = [];
  let shoppingList: FoodItem[] = [];
  let loading = false;
  let saving = false;
  let errorMessage: string | null = null;

  const emptyForm = () => ({
    name: "",
    location: activeLocation,
    quantity: 1,
    unit: "pcs",
    restockThreshold: 1,
    expiresAt: "",
    category: "",
    trackShoppingList: true,
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
        trackShoppingList: Boolean(form.trackShoppingList),
        category: form.category?.trim() || undefined,
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

  async function toggleShoppingList(item: FoodItem) {
    try {
      await updateItem(item.id, { trackShoppingList: !item.trackShoppingList });
      await reloadAll();
    } catch (error) {
      console.error(error);
      errorMessage = "Unable to update shopping preference.";
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
</script>

<main>
  <header>
    <h1>Food Inventory Manager</h1>
    <p>
      Track pantry, freezer, and fridge items. Anything at or below its threshold
      flows into the shopping list automatically.
    </p>
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
                  <div class="inline-actions">
                    <button class="secondary" on:click={() => toggleShoppingList(item)}>
                      {item.trackShoppingList ? "Hide from list" : "Track to buy"}
                    </button>
                    <button class="danger" on:click={() => removeItem(item)}>Delete</button>
                  </div>
                </td>
              </tr>
            {/each}
          </tbody>
        </table>
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
          <input id="unit" bind:value={form.unit} placeholder="packs" required />
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
          <input id="category" bind:value={form.category} placeholder="Produce" />
        </div>
        <div>
          <label for="notes">Notes</label>
          <textarea id="notes" bind:value={form.notes} rows="2" placeholder="Brand or flavor details" />
        </div>
        <div>
          <label>
            <input
              type="checkbox"
              bind:checked={form.trackShoppingList}
            />
            Track in shopping list
          </label>
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
