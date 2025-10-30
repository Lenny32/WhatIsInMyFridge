<script lang="ts">
  import { onMount } from "svelte";
  import type { Recipe, CreateRecipe, MeasurementUnit, CreateRecipeIngredient } from "./types";
  import type { components } from "./api-types";
  import {
    listRecipes,
    createRecipe,
    updateRecipe,
    deleteRecipe,
    getIngredientSuggestions,
    uploadRecipePhoto,
    deleteRecipePhoto,
    getPhotoUrl,
  } from "./api";

  type MUnit = components["schemas"]["MeasurementUnit"];

  const units: MUnit[] = [
    "Pieces", "Items",
    "Grams", "Kilograms", "Ounces", "Pounds",
    "Milliliters", "Liters", "FluidOunces",
    "Cups", "Pints", "Quarts", "Gallons",
    "Teaspoons", "Tablespoons",
    "Cans", "Bottles", "Jars", "Boxes", "Bags", "Packages", "Cartons",
    "Undefined"
  ];

  let recipes: Recipe[] = [];
  let loading = false;
  let saving = false;
  let errorMessage: string | null = null;
  let showForm = false;
  let editingId: string | null = null;
  let selectedRecipe: Recipe | null = null;

  const emptyForm = (): CreateRecipe => ({
    name: "",
    description: "",
    servings: 4,
    prepTimeMinutes: 15,
    cookTimeMinutes: 30,
    ingredients: [],
    instructions: [""],
    notes: ""
  });

  let form = emptyForm();
  let ingredientSuggestions: string[] = [];
  let activeIngredientIndex: number | null = null;
  let uploadingPhoto = false;
  let photoFiles: File[] = [];
  let isDragging = false;
  let photoInput: HTMLInputElement;

  onMount(async () => {
    await loadRecipes();
  });

  async function loadRecipes() {
    loading = true;
    errorMessage = null;

    try {
      recipes = await listRecipes();
    } catch (error) {
      console.error(error);
      errorMessage = "Unable to load recipes.";
    } finally {
      loading = false;
    }
  }

  async function handleSubmit() {
    if (!form.name.trim()) {
      errorMessage = "Recipe name is required.";
      return;
    }

    if (form.ingredients.length === 0) {
      errorMessage = "At least one ingredient is required.";
      return;
    }

    if (form.instructions.filter(i => i.trim()).length === 0) {
      errorMessage = "At least one instruction is required.";
      return;
    }

    saving = true;
    errorMessage = null;

    const payload: CreateRecipe = {
      ...form,
      ingredients: form.ingredients.filter(i => i.name.trim()),
      instructions: form.instructions.filter(i => i.trim())
    };

    try {
      if (editingId) {
        await updateRecipe(editingId, payload);
      } else {
        await createRecipe(payload);
      }
      await loadRecipes();
      resetForm();
    } catch (error: any) {
      console.error(error);
      errorMessage = error.message || "Failed to save recipe.";
    } finally {
      saving = false;
    }
  }

  async function handleDelete(id: string) {
    if (!confirm("Are you sure you want to delete this recipe?")) return;

    try {
      await deleteRecipe(id);
      await loadRecipes();
      if (selectedRecipe?.id === id) {
        selectedRecipe = null;
      }
    } catch (error: any) {
      console.error(error);
      errorMessage = error.message || "Failed to delete recipe.";
    }
  }

  function startEdit(recipe: Recipe) {
    editingId = recipe.id;
    form = {
      name: recipe.name,
      description: recipe.description || "",
      servings: recipe.servings,
      prepTimeMinutes: recipe.prepTimeMinutes,
      cookTimeMinutes: recipe.cookTimeMinutes,
      ingredients: recipe.ingredients.map(i => ({
        name: i.name,
        quantity: i.quantity,
        unit: i.unit,
        notes: i.notes || ""
      })),
      instructions: [...recipe.instructions],
      notes: recipe.notes || ""
    };
    showForm = true;
    selectedRecipe = null;
  }

  function resetForm() {
    form = emptyForm();
    editingId = null;
    showForm = false;
  }

  function addIngredient() {
    form.ingredients = [...form.ingredients, { name: "", quantity: 1, unit: "Pieces" }];
  }

  function removeIngredient(index: number) {
    form.ingredients = form.ingredients.filter((_, i) => i !== index);
  }

  function addInstruction() {
    form.instructions = [...form.instructions, ""];
  }

  function removeInstruction(index: number) {
    form.instructions = form.instructions.filter((_, i) => i !== index);
  }

  async function handleIngredientNameInput(index: number, value: string) {
    form.ingredients[index].name = value;
    
    if (value.length >= 2) {
      activeIngredientIndex = index;
      try {
        ingredientSuggestions = await getIngredientSuggestions(value);
      } catch (error) {
        console.error("Failed to fetch suggestions", error);
        ingredientSuggestions = [];
      }
    } else {
      ingredientSuggestions = [];
      activeIngredientIndex = null;
    }
  }

  function selectSuggestion(index: number, suggestion: string) {
    form.ingredients[index].name = suggestion;
    ingredientSuggestions = [];
    activeIngredientIndex = null;
  }

  function viewRecipe(recipe: Recipe) {
    selectedRecipe = recipe;
    showForm = false;
  }

  function formatTime(minutes: number): string {
    if (minutes === 0) return "0 min";
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (hours === 0) return `${mins} min`;
    if (mins === 0) return `${hours} hr`;
    return `${hours} hr ${mins} min`;
  }

  // Photo upload handlers
  function handleFileSelect(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      addPhotoFiles(Array.from(input.files));
    }
  }

  function addPhotoFiles(files: File[]) {
    const validFiles = files.filter(f => {
      const isImage = f.type.startsWith("image/");
      const isUnder5MB = f.size <= 5 * 1024 * 1024;
      return isImage && isUnder5MB;
    });

    const available = 4 - (selectedRecipe?.photos.length || 0) - photoFiles.length;
    photoFiles = [...photoFiles, ...validFiles].slice(0, available);
  }

  function removePhotoFile(index: number) {
    photoFiles = photoFiles.filter((_, i) => i !== index);
  }

  async function uploadPhotos() {
    if (!selectedRecipe || photoFiles.length === 0) return;

    uploadingPhoto = true;
    errorMessage = null;

    try {
      for (const file of photoFiles) {
        await uploadRecipePhoto(selectedRecipe.id, file);
      }
      photoFiles = [];
      await loadRecipes();
      // Refresh selected recipe
      if (selectedRecipe) {
        const updated = recipes.find(r => r.id === selectedRecipe!.id);
        if (updated) selectedRecipe = updated;
      }
    } catch (error: any) {
      errorMessage = error.message || "Failed to upload photos";
    } finally {
      uploadingPhoto = false;
    }
  }

  async function handleDeletePhoto(photoId: string) {
    if (!selectedRecipe || !confirm("Delete this photo?")) return;

    try {
      await deleteRecipePhoto(selectedRecipe.id, photoId);
      await loadRecipes();
      if (selectedRecipe) {
        const updated = recipes.find(r => r.id === selectedRecipe!.id);
        if (updated) selectedRecipe = updated;
      }
    } catch (error: any) {
      errorMessage = error.message || "Failed to delete photo";
    }
  }

  // Drag and drop handlers
  function handleDragOver(event: DragEvent) {
    event.preventDefault();
    isDragging = true;
  }

  function handleDragLeave(event: DragEvent) {
    event.preventDefault();
    isDragging = false;
  }

  function handleDrop(event: DragEvent) {
    event.preventDefault();
    isDragging = false;

    if (event.dataTransfer?.files) {
      addPhotoFiles(Array.from(event.dataTransfer.files));
    }
  }

</script>

<main>
  <header>
    <h1>My Recipes</h1>
    <button class="btn-primary" on:click={() => { showForm = true; selectedRecipe = null; }}>
      + Add Recipe
    </button>
  </header>

  {#if errorMessage}
    <div class="error-message">{errorMessage}</div>
  {/if}

  <div class="content">
    <div class="recipes-list">
      {#if loading}
        <p class="loading">Loading recipes...</p>
      {:else if recipes.length === 0}
        <p class="empty-state">No recipes yet. Add your first recipe!</p>
      {:else}
        {#each recipes as recipe (recipe.id)}
          <div 
            class="recipe-card" 
            class:selected={selectedRecipe?.id === recipe.id}
            on:click={() => viewRecipe(recipe)}
          >
            <h3>{recipe.name}</h3>
            {#if recipe.description}
              <p class="description">{recipe.description}</p>
            {/if}
            <div class="meta">
              <span>{recipe.servings} servings</span>
              <span>{formatTime(recipe.prepTimeMinutes + recipe.cookTimeMinutes)}</span>
              <span>{recipe.ingredients.length} ingredients</span>
            </div>
          </div>
        {/each}
      {/if}
    </div>

    <div class="detail-panel">
      {#if showForm}
        <div class="recipe-form">
          <h2>{editingId ? 'Edit Recipe' : 'New Recipe'}</h2>
          
          <form on:submit|preventDefault={handleSubmit}>
            <div class="form-group">
              <label for="name">Recipe Name *</label>
              <input type="text" id="name" bind:value={form.name} required />
            </div>

            <div class="form-group">
              <label for="description">Description</label>
              <textarea id="description" bind:value={form.description} rows="2"></textarea>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label for="servings">Servings *</label>
                <input type="number" id="servings" bind:value={form.servings} min="1" required />
              </div>
              <div class="form-group">
                <label for="prep">Prep Time (min)</label>
                <input type="number" id="prep" bind:value={form.prepTimeMinutes} min="0" />
              </div>
              <div class="form-group">
                <label for="cook">Cook Time (min)</label>
                <input type="number" id="cook" bind:value={form.cookTimeMinutes} min="0" />
              </div>
            </div>

            <div class="form-section">
              <h3>Ingredients *</h3>
              {#each form.ingredients as ingredient, i}
                <div class="ingredient-row">
                  <div class="ingredient-name ingredient-field">
                    <label class="mobile-label">Name</label>
                    <input 
                      type="text" 
                      placeholder="Ingredient name" 
                      value={ingredient.name}
                      on:input={(e) => handleIngredientNameInput(i, e.currentTarget.value)}
                      required 
                    />
                    {#if ingredientSuggestions.length > 0 && activeIngredientIndex === i}
                      <div class="suggestions">
                        {#each ingredientSuggestions as suggestion}
                          <button 
                            type="button" 
                            class="suggestion-item"
                            on:click={() => selectSuggestion(i, suggestion)}
                          >
                            {suggestion}
                          </button>
                        {/each}
                      </div>
                    {/if}
                  </div>
                  <div class="ingredient-field">
                    <label class="mobile-label">Quantity</label>
                    <input 
                      type="number" 
                      placeholder="Qty" 
                      bind:value={ingredient.quantity} 
                      min="1" 
                      step="1"
                      required 
                    />
                  </div>
                  <div class="ingredient-field">
                    <label class="mobile-label">Unit</label>
                    <select bind:value={ingredient.unit} required>
                      {#each units as unit}
                        <option value={unit}>{unit}</option>
                      {/each}
                    </select>
                  </div>
                  <div class="ingredient-field">
                    <label class="mobile-label">Notes</label>
                    <input 
                      type="text" 
                      placeholder="Notes (optional)" 
                      bind:value={ingredient.notes}
                    />
                  </div>
                  <button type="button" class="btn-remove" on:click={() => removeIngredient(i)}>×</button>
                </div>
              {/each}
              <button type="button" class="btn-secondary" on:click={addIngredient}>+ Add Ingredient</button>
            </div>

            <div class="form-section">
              <h3>Instructions *</h3>
              {#each form.instructions as instruction, i}
                <div class="instruction-row">
                  <span class="step-number">Step {i + 1}</span>
                  <div class="instruction-field">
                    <textarea 
                      placeholder="Instruction step" 
                      bind:value={form.instructions[i]}
                      rows="2"
                      required
                    ></textarea>
                  </div>
                  <button type="button" class="btn-remove" on:click={() => removeInstruction(i)}>×</button>
                </div>
              {/each}
              <button type="button" class="btn-secondary" on:click={addInstruction}>+ Add Step</button>
            </div>

            <div class="form-group">
              <label for="notes">Notes</label>
              <textarea id="notes" bind:value={form.notes} rows="2"></textarea>
            </div>

            <div class="form-actions">
              <button type="button" class="btn-secondary" on:click={resetForm}>Cancel</button>
              <button type="submit" class="btn-primary" disabled={saving}>
                {saving ? 'Saving...' : editingId ? 'Update Recipe' : 'Add Recipe'}
              </button>
            </div>
          </form>
        </div>
      {:else if selectedRecipe}
        <div class="recipe-detail">
          <div class="detail-header">
            <h2>{selectedRecipe.name}</h2>
            <div class="detail-actions">
              <button class="btn-secondary" on:click={() => selectedRecipe && startEdit(selectedRecipe)}>Edit</button>
              <button class="btn-danger" on:click={() => selectedRecipe && handleDelete(selectedRecipe.id)}>Delete</button>
            </div>
          </div>

          {#if selectedRecipe.description}
            <p class="description">{selectedRecipe.description}</p>
          {/if}

          <!-- Photos Section -->
          {#if selectedRecipe.photos && selectedRecipe.photos.length > 0}
            <div class="photo-gallery">
              {#each selectedRecipe.photos as photoId}
                <div class="photo-item">
                  <img src={getPhotoUrl(photoId)} alt="Recipe photo" />
                  <button 
                    class="photo-delete-btn" 
                    on:click={() => handleDeletePhoto(photoId)}
                    title="Delete photo"
                  >×</button>
                </div>
              {/each}
            </div>
          {/if}

          <!-- Photo Upload Section -->
          {#if selectedRecipe.photos.length < 4}
            <div class="photo-upload-section">
              <h3>Add Photos ({selectedRecipe.photos.length}/4)</h3>
              
              <div 
                class="drop-zone" 
                class:dragging={isDragging}
                on:dragover={handleDragOver}
                on:dragleave={handleDragLeave}
                on:drop={handleDrop}
                on:click={() => photoInput.click()}
              >
                <p>📸 Drop photos here or click to select</p>
                <p class="drop-zone-hint">Max 4 photos, 5MB each (JPEG, PNG, WebP)</p>
              </div>

              <input 
                type="file" 
                bind:this={photoInput}
                on:change={handleFileSelect}
                accept="image/jpeg,image/jpg,image/png,image/webp"
                multiple
                style="display: none;"
              />

              {#if photoFiles.length > 0}
                <div class="photo-preview-list">
                  {#each photoFiles as file, i}
                    <div class="photo-preview-item">
                      <img src={URL.createObjectURL(file)} alt="Preview" />
                      <button class="photo-remove-btn" on:click={() => removePhotoFile(i)}>×</button>
                      <span class="photo-name">{file.name}</span>
                    </div>
                  {/each}
                </div>
                <button 
                  class="btn-primary" 
                  on:click={uploadPhotos}
                  disabled={uploadingPhoto}
                >
                  {uploadingPhoto ? 'Uploading...' : `Upload ${photoFiles.length} photo${photoFiles.length > 1 ? 's' : ''}`}
                </button>
              {/if}
            </div>
          {/if}

          <div class="recipe-meta">
            <div class="meta-item">
              <strong>Servings:</strong> {selectedRecipe.servings}
            </div>
            <div class="meta-item">
              <strong>Prep:</strong> {formatTime(selectedRecipe.prepTimeMinutes)}
            </div>
            <div class="meta-item">
              <strong>Cook:</strong> {formatTime(selectedRecipe.cookTimeMinutes)}
            </div>
            <div class="meta-item">
              <strong>Total:</strong> {formatTime(selectedRecipe.prepTimeMinutes + selectedRecipe.cookTimeMinutes)}
            </div>
          </div>

          <section>
            <h3>Ingredients</h3>
            <ul class="ingredients-list">
              {#each selectedRecipe.ingredients as ingredient}
                <li>
                  <strong>{ingredient.quantity} {ingredient.unit}</strong> {ingredient.name}
                  {#if ingredient.notes}
                    <em>({ingredient.notes})</em>
                  {/if}
                </li>
              {/each}
            </ul>
          </section>

          <section>
            <h3>Instructions</h3>
            <ol class="instructions-list">
              {#each selectedRecipe.instructions as instruction}
                <li>{instruction}</li>
              {/each}
            </ol>
          </section>

          {#if selectedRecipe.notes}
            <section>
              <h3>Notes</h3>
              <p>{selectedRecipe.notes}</p>
            </section>
          {/if}
        </div>
      {:else}
        <div class="empty-detail">
          <p>Select a recipe to view details or add a new recipe</p>
        </div>
      {/if}
    </div>
  </div>
</main>

<style>
  main {
    max-width: 1400px;
    margin: 0 auto;
    padding: 2rem;
  }

  header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 2rem;
  }

  h1 {
    font-size: 2rem;
    margin: 0;
  }

  .content {
    display: grid;
    grid-template-columns: 400px 1fr;
    gap: 2rem;
  }

  .recipes-list {
    display: flex;
    flex-direction: column;
    gap: 1rem;
    max-height: calc(100vh - 200px);
    overflow-y: auto;
  }

  .recipe-card {
    background: white;
    border: 1px solid #e5e7eb;
    border-radius: 8px;
    padding: 1rem;
    cursor: pointer;
    transition: all 0.2s;
  }

  .recipe-card:hover {
    border-color: #3b82f6;
    box-shadow: 0 2px 8px rgba(59, 130, 246, 0.1);
  }

  .recipe-card.selected {
    border-color: #3b82f6;
    background: #eff6ff;
  }

  .recipe-card h3 {
    margin: 0 0 0.5rem 0;
    font-size: 1.125rem;
  }

  .recipe-card .description {
    margin: 0 0 0.5rem 0;
    color: #6b7280;
    font-size: 0.875rem;
    display: -webkit-box;
    -webkit-line-clamp: 2;
    -webkit-box-orient: vertical;
    overflow: hidden;
  }

  .meta {
    display: flex;
    gap: 1rem;
    font-size: 0.875rem;
    color: #6b7280;
  }

  .detail-panel {
    background: white;
    border: 1px solid #e5e7eb;
    border-radius: 8px;
    padding: 2rem;
    max-height: calc(100vh - 200px);
    overflow-y: auto;
  }

  .recipe-detail h2 {
    margin: 0 0 1rem 0;
  }

  .detail-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 1rem;
  }

  .detail-actions {
    display: flex;
    gap: 0.5rem;
  }

  .recipe-meta {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 1rem;
    margin: 1rem 0;
    padding: 1rem;
    background: #f9fafb;
    border-radius: 6px;
  }

  .meta-item {
    font-size: 0.875rem;
  }

  section {
    margin: 1.5rem 0;
  }

  section h3 {
    margin: 0 0 0.75rem 0;
    font-size: 1.125rem;
  }

  .ingredients-list {
    list-style: none;
    padding: 0;
    margin: 0;
  }

  .ingredients-list li {
    padding: 0.5rem 0;
    border-bottom: 1px solid #f3f4f6;
  }

  .ingredients-list li:last-child {
    border-bottom: none;
  }

  .instructions-list {
    padding-left: 1.5rem;
    margin: 0;
  }

  .instructions-list li {
    padding: 0.5rem 0;
    margin-bottom: 0.5rem;
  }

  .recipe-form h2 {
    margin: 0 0 1.5rem 0;
  }

  .form-group {
    margin-bottom: 1rem;
  }

  .form-group label {
    display: block;
    margin-bottom: 0.5rem;
    font-weight: 500;
    color: #374151;
  }

  .form-group input,
  .form-group textarea,
  .form-group select {
    width: 100%;
    padding: 0.5rem;
    border: 1px solid #d1d5db;
    border-radius: 6px;
    font-size: 1rem;
  }

  .form-row {
    display: grid;
    grid-template-columns: repeat(3, 1fr);
    gap: 1rem;
  }

  .form-section {
    margin: 1.5rem 0;
  }

  .form-section h3 {
    margin: 0 0 1rem 0;
    font-size: 1.125rem;
  }

  .ingredient-row {
    display: grid;
    grid-template-columns: 2fr 1fr 1.5fr 1.5fr auto;
    gap: 0.5rem;
    margin-bottom: 0.5rem;
    align-items: start;
  }

  .ingredient-name {
    position: relative;
  }

  .ingredient-field {
    display: flex;
    flex-direction: column;
  }

  .mobile-label {
    display: none;
    font-size: 0.75rem;
    font-weight: 500;
    color: #6b7280;
    margin-bottom: 0.25rem;
  }

  .suggestions {
    position: absolute;
    top: 100%;
    left: 0;
    right: 0;
    background: white;
    border: 1px solid #d1d5db;
    border-radius: 6px;
    margin-top: 2px;
    box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
    z-index: 10;
    max-height: 200px;
    overflow-y: auto;
  }

  .suggestion-item {
    width: 100%;
    padding: 0.5rem;
    border: none;
    background: white;
    text-align: left;
    cursor: pointer;
    font-size: 0.875rem;
  }

  .suggestion-item:hover {
    background: #f3f4f6;
  }

  .instruction-row {
    display: grid;
    grid-template-columns: auto 1fr auto;
    gap: 0.5rem;
    margin-bottom: 0.5rem;
    align-items: start;
  }

  .instruction-field {
    display: flex;
    flex-direction: column;
    flex: 1;
  }

  .step-number {
    padding: 0.5rem 0;
    font-weight: 500;
  }

  .form-actions {
    display: flex;
    gap: 1rem;
    justify-content: flex-end;
    margin-top: 1.5rem;
  }

  .btn-primary {
    background: #3b82f6;
    color: white;
    border: none;
    padding: 0.5rem 1rem;
    border-radius: 6px;
    cursor: pointer;
    font-size: 1rem;
    font-weight: 500;
  }

  .btn-primary:hover:not(:disabled) {
    background: #2563eb;
  }

  .btn-primary:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  .btn-secondary {
    background: #f3f4f6;
    color: #374151;
    border: 1px solid #d1d5db;
    padding: 0.5rem 1rem;
    border-radius: 6px;
    cursor: pointer;
    font-size: 1rem;
    font-weight: 500;
  }

  .btn-secondary:hover {
    background: #e5e7eb;
  }

  .btn-danger {
    background: #ef4444;
    color: white;
    border: none;
    padding: 0.5rem 1rem;
    border-radius: 6px;
    cursor: pointer;
    font-size: 1rem;
    font-weight: 500;
  }

  .btn-danger:hover {
    background: #dc2626;
  }

  .btn-remove {
    background: #fee2e2;
    color: #dc2626;
    border: none;
    padding: 0.5rem;
    border-radius: 6px;
    cursor: pointer;
    font-size: 1.25rem;
    line-height: 1;
    width: 32px;
    height: 32px;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .btn-remove:hover {
    background: #fecaca;
  }

  .error-message {
    background: #fee2e2;
    color: #dc2626;
    padding: 1rem;
    border-radius: 6px;
    margin-bottom: 1rem;
  }

  .loading,
  .empty-state,
  .empty-detail {
    text-align: center;
    color: #6b7280;
    padding: 2rem;
  }

  @media (max-width: 1024px) {
    .content {
      grid-template-columns: 1fr;
    }

    .recipes-list {
      max-height: 400px;
    }

    .detail-panel {
      max-height: none;
    }
  }

  @media (max-width: 767px) {
    main {
      padding: 1rem;
    }

    header {
      flex-direction: column;
      align-items: flex-start;
      gap: 1rem;
    }

    h1 {
      font-size: 1.5rem;
    }

    .btn-primary {
      width: 100%;
    }

    .detail-panel {
      padding: 1rem;
    }

    .detail-header {
      flex-direction: column;
      align-items: flex-start;
      gap: 1rem;
    }

    .detail-header h2 {
      margin: 0;
    }

    .detail-actions {
      width: 100%;
    }

    .detail-actions button {
      flex: 1;
    }

    .recipe-meta {
      grid-template-columns: 1fr;
      gap: 0.5rem;
    }

    .form-row {
      grid-template-columns: 1fr;
    }

    .mobile-label {
      display: block;
    }

    .ingredient-row {
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
      padding: 1rem;
      background: #f9fafb;
      border-radius: 8px;
      margin-bottom: 1rem;
      position: relative;
    }

    .ingredient-row .btn-remove {
      position: absolute;
      top: 0.5rem;
      right: 0.5rem;
    }

    .ingredient-field {
      width: 100%;
    }

    .ingredient-field input,
    .ingredient-field select {
      width: 100%;
    }

    .instruction-row {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      padding: 1rem;
      background: #f9fafb;
      border-radius: 8px;
      margin-bottom: 1rem;
      position: relative;
    }

    .instruction-row .btn-remove {
      position: absolute;
      top: 0.5rem;
      right: 0.5rem;
    }

    .step-number {
      padding: 0;
      font-size: 0.875rem;
      color: #6b7280;
    }

    .instruction-row textarea {
      min-height: 80px;
      width: 100%;
    }

    .form-actions {
      flex-direction: column-reverse;
    }

    .form-actions button {
      width: 100%;
    }

    .photo-gallery {
      grid-template-columns: repeat(2, 1fr);
      gap: 0.5rem;
    }

    .photo-delete-btn {
      opacity: 1;
    }

    .drop-zone {
      padding: 1.5rem 1rem;
    }

    .photo-preview-list {
      grid-template-columns: repeat(2, 1fr);
      gap: 0.5rem;
    }

    .meta {
      flex-wrap: wrap;
      gap: 0.5rem;
    }

    .recipes-list {
      max-height: 300px;
    }
  }

  /* Photo styles */
  .photo-gallery {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
    gap: 1rem;
    margin: 1rem 0;
  }

  .photo-item {
    position: relative;
    aspect-ratio: 1;
    border-radius: 8px;
    overflow: hidden;
    border: 1px solid #e5e7eb;
  }

  .photo-item img {
    width: 100%;
    height: 100%;
    object-fit: cover;
  }

  .photo-delete-btn {
    position: absolute;
    top: 0.5rem;
    right: 0.5rem;
    background: rgba(239, 68, 68, 0.9);
    color: white;
    border: none;
    border-radius: 50%;
    width: 28px;
    height: 28px;
    cursor: pointer;
    font-size: 1.25rem;
    line-height: 1;
    display: flex;
    align-items: center;
    justify-content: center;
    opacity: 0;
    transition: opacity 0.2s;
  }

  .photo-item:hover .photo-delete-btn {
    opacity: 1;
  }

  .photo-delete-btn:hover {
    background: rgba(220, 38, 38, 0.9);
  }

  .photo-upload-section {
    margin: 1.5rem 0;
  }

  .photo-upload-section h3 {
    margin: 0 0 1rem 0;
    font-size: 1.125rem;
  }

  .drop-zone {
    border: 2px dashed #d1d5db;
    border-radius: 8px;
    padding: 2rem;
    text-align: center;
    cursor: pointer;
    transition: all 0.2s;
    background: #f9fafb;
  }

  .drop-zone:hover,
  .drop-zone.dragging {
    border-color: #3b82f6;
    background: #eff6ff;
  }

  .drop-zone p {
    margin: 0.25rem 0;
    color: #6b7280;
  }

  .drop-zone-hint {
    font-size: 0.875rem;
  }

  .photo-preview-list {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(120px, 1fr));
    gap: 1rem;
    margin: 1rem 0;
  }

  .photo-preview-item {
    position: relative;
    display: flex;
    flex-direction: column;
    gap: 0.5rem;
  }

  .photo-preview-item img {
    width: 100%;
    aspect-ratio: 1;
    object-fit: cover;
    border-radius: 6px;
    border: 1px solid #e5e7eb;
  }

  .photo-remove-btn {
    position: absolute;
    top: 0.25rem;
    right: 0.25rem;
    background: rgba(239, 68, 68, 0.9);
    color: white;
    border: none;
    border-radius: 50%;
    width: 24px;
    height: 24px;
    cursor: pointer;
    font-size: 1rem;
    line-height: 1;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .photo-remove-btn:hover {
    background: rgba(220, 38, 38, 0.9);
  }

  .photo-name {
    font-size: 0.75rem;
    color: #6b7280;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

</style>
