<script lang="ts">
  import { onMount, onDestroy } from "svelte";
  import Calendar from "@event-calendar/core";
  import type { MealPlan, Recipe } from "./types";
  import {
    listMealPlans,
    createMealPlan,
    updateMealPlan,
    deleteMealPlan,
    listRecipes,
  } from "./api";

  let recipes: Recipe[] = [];
  let mealPlans: MealPlan[] = [];
  let loading = false;
  let errorMessage: string | null = null;
  
  let calendarEl: HTMLDivElement;
  let calendar: any;
  
  let showAddMealModal = false;
  let showRecipeDetailsModal = false;
  let selectedDate: Date | null = null;
  let selectedRecipeId: string = "";
  let selectedMealPlan: MealPlan | null = null;
  let mealName: string = "";
  let mealNotes: string = "";
  
  // Orientation tracking
  let isPortrait = true;
  let isMobile = false;

  onMount(async () => {
    // Check if mobile
    isMobile = window.matchMedia("(max-width: 767px)").matches;
    isPortrait = window.matchMedia("(orientation: portrait)").matches;
    
    // Listen for orientation changes
    const orientationQuery = window.matchMedia("(orientation: portrait)");
    const handleOrientationChange = (e: MediaQueryListEvent) => {
      isPortrait = e.matches;
      updateCalendarView();
    };
    orientationQuery.addEventListener("change", handleOrientationChange);
    
    await loadData();
    initCalendar();
    
    return () => {
      orientationQuery.removeEventListener("change", handleOrientationChange);
      if (calendar) {
        calendar.destroy();
      }
    };
  });

  onDestroy(() => {
    if (calendar) {
      calendar.destroy();
    }
  });

  async function loadData() {
    loading = true;
    errorMessage = null;

    try {
      const today = new Date();
      const startDate = new Date(today.getFullYear(), today.getMonth(), 1);
      const endDate = new Date(today.getFullYear(), today.getMonth() + 2, 0);
      
      [recipes, mealPlans] = await Promise.all([
        listRecipes(),
        listMealPlans(
          startDate.toISOString().split("T")[0],
          endDate.toISOString().split("T")[0]
        ),
      ]);
    } catch (error) {
      console.error(error);
      errorMessage = "Unable to load meal plans or recipes.";
    } finally {
      loading = false;
    }
  }

  function initCalendar() {
    const view = getCalendarView();
    
    calendar = new Calendar({
      target: calendarEl,
      props: {
        options: {
          view: view,
          height: "auto",
          headerToolbar: {
            start: "prev,next today",
            center: "title",
            end: "dayGridMonth,timeGridWeek,timeGridDay",
          },
          buttonText: {
            today: "Today",
            dayGridMonth: "Month",
            timeGridWeek: "Week",
            timeGridDay: "Day",
          },
          events: mealPlansToEvents(),
          eventClick: handleEventClick,
          dateClick: handleDateClick,
          allDaySlot: true,
          eventTimeFormat: {
            hour: "numeric",
            minute: "2-digit",
            meridiem: "short",
          },
          slotDuration: "01:00:00",
          editable: false,
          eventStartEditable: false,
          eventDurationEditable: false,
        },
      },
    });
  }

  function getCalendarView(): string {
    if (isMobile) {
      return isPortrait ? "timeGridDay" : "timeGridWeek";
    }
    return "dayGridMonth";
  }

  function updateCalendarView() {
    if (calendar) {
      const view = getCalendarView();
      calendar.setOption("view", view);
    }
  }

  function mealPlansToEvents() {
    return mealPlans.map((mp) => {
      const recipe = mp.recipe;
      const title = recipe ? recipe.name : "Unknown Recipe";
      const color = recipe ? "#3b82f6" : "#9ca3af";
      
      return {
        id: mp.id,
        title: mp.mealName ? `${mp.mealName}: ${title}` : title,
        start: mp.plannedDate,
        allDay: true,
        backgroundColor: color,
        borderColor: color,
        extendedProps: {
          mealPlan: mp,
        },
      };
    });
  }

  function handleEventClick(info: any) {
    const mealPlan = info.event.extendedProps.mealPlan as MealPlan;
    selectedMealPlan = mealPlan;
    showRecipeDetailsModal = true;
  }

  function handleDateClick(info: any) {
    selectedDate = info.date;
    selectedRecipeId = "";
    mealName = "";
    mealNotes = "";
    showAddMealModal = true;
  }

  async function handleAddMeal() {
    if (!selectedDate || !selectedRecipeId) {
      errorMessage = "Please select a recipe.";
      return;
    }

    loading = true;
    errorMessage = null;

    try {
      const dateStr = selectedDate.toISOString().split("T")[0];
      await createMealPlan({
        recipeId: selectedRecipeId,
        plannedDate: dateStr,
        mealName: mealName || undefined,
        notes: mealNotes || undefined,
      });

      showAddMealModal = false;
      await loadData();
      
      // Update calendar events
      if (calendar) {
        calendar.setOption("events", mealPlansToEvents());
      }
    } catch (error) {
      console.error(error);
      errorMessage = "Failed to add meal plan.";
    } finally {
      loading = false;
    }
  }

  async function handleDeleteMeal(id: string) {
    if (!confirm("Are you sure you want to delete this meal plan?")) {
      return;
    }

    loading = true;
    errorMessage = null;

    try {
      await deleteMealPlan(id);
      showRecipeDetailsModal = false;
      selectedMealPlan = null;
      await loadData();
      
      // Update calendar events
      if (calendar) {
        calendar.setOption("events", mealPlansToEvents());
      }
    } catch (error) {
      console.error(error);
      errorMessage = "Failed to delete meal plan.";
    } finally {
      loading = false;
    }
  }

  function closeAddMealModal() {
    showAddMealModal = false;
    selectedDate = null;
    selectedRecipeId = "";
    mealName = "";
    mealNotes = "";
    errorMessage = null;
  }

  function closeRecipeDetailsModal() {
    showRecipeDetailsModal = false;
    selectedMealPlan = null;
    errorMessage = null;
  }

  function formatDate(dateStr: string): string {
    const date = new Date(dateStr + "T00:00:00");
    return date.toLocaleDateString("en-US", {
      weekday: "long",
      year: "numeric",
      month: "long",
      day: "numeric",
    });
  }

  function copyGroceryList() {
    if (!selectedMealPlan?.recipe) return;
    
    const recipe = selectedMealPlan.recipe;
    const ingredients = recipe.ingredients
      .map((i) => `${i.quantity} ${i.unit} ${i.name}${i.notes ? ` (${i.notes})` : ""}`)
      .join("\n");
    
    const text = `Grocery List for ${recipe.name}\n\n${ingredients}`;
    
    navigator.clipboard.writeText(text).then(() => {
      alert("Grocery list copied to clipboard!");
    }).catch((err) => {
      console.error("Failed to copy:", err);
      alert("Failed to copy grocery list.");
    });
  }
</script>

{#if loading && !calendar}
  <div class="loading-container">
    <p>Loading meal plans...</p>
  </div>
{:else}
  <div class="meal-prep-container">
    <div class="header">
      <h2>Meal Prep Calendar</h2>
      <p class="subtitle">Plan your meals and organize your grocery shopping</p>
    </div>

    {#if errorMessage}
      <div class="error-message">
        {errorMessage}
        <button type="button" on:click={() => errorMessage = null}>✕</button>
      </div>
    {/if}

    <div class="calendar-wrapper">
      <div bind:this={calendarEl}></div>
    </div>

    {#if isMobile}
      <div class="mobile-hint">
        {#if isPortrait}
          <p>📱 Rotate your device to landscape for week view</p>
        {:else}
          <p>📱 Day view in portrait | Week view in landscape</p>
        {/if}
      </div>
    {/if}
  </div>
{/if}

<!-- Add Meal Modal -->
{#if showAddMealModal}
  <div class="modal-backdrop" on:click={closeAddMealModal}>
    <div class="modal" on:click|stopPropagation>
      <div class="modal-header">
        <h3>Add Meal Plan</h3>
        <button type="button" class="close-btn" on:click={closeAddMealModal}>✕</button>
      </div>

      <div class="modal-body">
        {#if selectedDate}
          <p class="selected-date">{formatDate(selectedDate.toISOString().split("T")[0])}</p>
        {/if}

        <div class="form-group">
          <label for="recipe-select">Recipe *</label>
          <select id="recipe-select" bind:value={selectedRecipeId} required>
            <option value="">Select a recipe...</option>
            {#each recipes as recipe}
              <option value={recipe.id}>{recipe.name}</option>
            {/each}
          </select>
        </div>

        <div class="form-group">
          <label for="meal-name">Meal Name (optional)</label>
          <input
            id="meal-name"
            type="text"
            bind:value={mealName}
            placeholder="e.g., Breakfast, Lunch, Dinner"
          />
        </div>

        <div class="form-group">
          <label for="meal-notes">Notes (optional)</label>
          <textarea
            id="meal-notes"
            bind:value={mealNotes}
            rows="3"
            placeholder="Any special notes..."
          ></textarea>
        </div>
      </div>

      <div class="modal-footer">
        <button type="button" class="btn-secondary" on:click={closeAddMealModal}>
          Cancel
        </button>
        <button
          type="button"
          class="btn-primary"
          on:click={handleAddMeal}
          disabled={loading || !selectedRecipeId}
        >
          {loading ? "Adding..." : "Add Meal"}
        </button>
      </div>
    </div>
  </div>
{/if}

<!-- Recipe Details Modal -->
{#if showRecipeDetailsModal && selectedMealPlan}
  <div class="modal-backdrop" on:click={closeRecipeDetailsModal}>
    <div class="modal modal-large" on:click|stopPropagation>
      <div class="modal-header">
        <h3>Meal Details</h3>
        <button type="button" class="close-btn" on:click={closeRecipeDetailsModal}>✕</button>
      </div>

      <div class="modal-body">
        <div class="meal-info">
          <p><strong>Date:</strong> {formatDate(selectedMealPlan.plannedDate)}</p>
          {#if selectedMealPlan.mealName}
            <p><strong>Meal:</strong> {selectedMealPlan.mealName}</p>
          {/if}
          {#if selectedMealPlan.notes}
            <p><strong>Notes:</strong> {selectedMealPlan.notes}</p>
          {/if}
        </div>

        {#if selectedMealPlan.recipe}
          {@const recipe = selectedMealPlan.recipe}
          <div class="recipe-details">
            <h4>{recipe.name}</h4>
            
            {#if recipe.description}
              <p class="recipe-description">{recipe.description}</p>
            {/if}

            <div class="recipe-meta">
              <span>🍽️ Servings: {recipe.servings}</span>
              <span>⏱️ Prep: {recipe.prepTimeMinutes} min</span>
              <span>🔥 Cook: {recipe.cookTimeMinutes} min</span>
            </div>

            <div class="ingredients-section">
              <h5>Ingredients</h5>
              <ul>
                {#each recipe.ingredients as ingredient}
                  <li>
                    {ingredient.quantity} {ingredient.unit} {ingredient.name}
                    {#if ingredient.notes}
                      <span class="ingredient-notes">({ingredient.notes})</span>
                    {/if}
                  </li>
                {/each}
              </ul>
              <button type="button" class="btn-copy" on:click={copyGroceryList}>
                📋 Copy Grocery List
              </button>
            </div>

            <div class="instructions-section">
              <h5>Instructions</h5>
              <ol>
                {#each recipe.instructions as instruction, i}
                  <li>{instruction}</li>
                {/each}
              </ol>
            </div>

            {#if recipe.notes}
              <div class="recipe-notes">
                <h5>Recipe Notes</h5>
                <p>{recipe.notes}</p>
              </div>
            {/if}
          </div>
        {:else}
          <p class="no-recipe">Recipe not found.</p>
        {/if}
      </div>

      <div class="modal-footer">
        <button
          type="button"
          class="btn-danger"
          on:click={() => handleDeleteMeal(selectedMealPlan!.id)}
          disabled={loading}
        >
          {loading ? "Deleting..." : "Delete Meal Plan"}
        </button>
        <button type="button" class="btn-secondary" on:click={closeRecipeDetailsModal}>
          Close
        </button>
      </div>
    </div>
  </div>
{/if}

<style>
  .loading-container {
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 400px;
    font-size: 1.125rem;
    color: var(--text-tertiary);
  }

  .meal-prep-container {
    padding: 2rem;
    max-width: 1400px;
    margin: 0 auto;
  }

  .header {
    margin-bottom: 2rem;
  }

  .header h2 {
    font-size: 2rem;
    font-weight: 700;
    color: var(--text-primary);
    margin-bottom: 0.5rem;
  }

  .subtitle {
    font-size: 1rem;
    color: var(--text-tertiary);
  }

  .error-message {
    background-color: #fee;
    border: 1px solid #fcc;
    color: #c33;
    padding: 1rem;
    border-radius: 6px;
    margin-bottom: 1rem;
    display: flex;
    justify-content: space-between;
    align-items: center;
  }

  .error-message button {
    background: none;
    border: none;
    font-size: 1.25rem;
    cursor: pointer;
    color: #c33;
  }

  .calendar-wrapper {
    background: white;
    border-radius: 8px;
    box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
    padding: 1rem;
    margin-bottom: 1rem;
  }

  .mobile-hint {
    text-align: center;
    padding: 0.5rem;
    background: #f0f9ff;
    border-radius: 6px;
    color: #0369a1;
    font-size: 0.875rem;
  }

  /* Modal Styles */
  .modal-backdrop {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
    padding: 1rem;
  }

  .modal {
    background: white;
    border-radius: 8px;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.2);
    width: 100%;
    max-width: 500px;
    max-height: 90vh;
    overflow-y: auto;
  }

  .modal-large {
    max-width: 700px;
  }

  .modal-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 1.5rem;
    border-bottom: 1px solid #e5e7eb;
  }

  .modal-header h3 {
    font-size: 1.5rem;
    font-weight: 700;
    color: var(--text-primary);
    margin: 0;
  }

  .close-btn {
    background: none;
    border: none;
    font-size: 1.5rem;
    cursor: pointer;
    color: var(--text-tertiary);
    padding: 0;
    width: 32px;
    height: 32px;
  }

  .close-btn:hover {
    color: var(--text-primary);
  }

  .modal-body {
    padding: 1.5rem;
  }

  .selected-date {
    font-size: 1.125rem;
    font-weight: 600;
    color: var(--text-primary);
    margin-bottom: 1rem;
  }

  .form-group {
    margin-bottom: 1rem;
  }

  .form-group label {
    display: block;
    font-weight: 600;
    color: var(--text-primary);
    margin-bottom: 0.5rem;
  }

  .form-group input,
  .form-group select,
  .form-group textarea {
    width: 100%;
    padding: 0.75rem;
    border: 1px solid #d1d5db;
    border-radius: 6px;
    font-size: 1rem;
    font-family: inherit;
  }

  .form-group input:focus,
  .form-group select:focus,
  .form-group textarea:focus {
    outline: none;
    border-color: #3b82f6;
    box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
  }

  .modal-footer {
    display: flex;
    justify-content: flex-end;
    gap: 0.75rem;
    padding: 1.5rem;
    border-top: 1px solid #e5e7eb;
  }

  .btn-primary,
  .btn-secondary,
  .btn-danger,
  .btn-copy {
    padding: 0.75rem 1.5rem;
    border-radius: 6px;
    font-size: 1rem;
    font-weight: 500;
    cursor: pointer;
    border: none;
    transition: all 0.2s;
  }

  .btn-primary {
    background: #3b82f6;
    color: white;
  }

  .btn-primary:hover:not(:disabled) {
    background: #2563eb;
  }

  .btn-primary:disabled {
    background: #9ca3af;
    cursor: not-allowed;
  }

  .btn-secondary {
    background: #f3f4f6;
    color: var(--text-primary);
  }

  .btn-secondary:hover {
    background: #e5e7eb;
  }

  .btn-danger {
    background: #ef4444;
    color: white;
  }

  .btn-danger:hover:not(:disabled) {
    background: #dc2626;
  }

  .btn-danger:disabled {
    background: #9ca3af;
    cursor: not-allowed;
  }

  .btn-copy {
    background: #10b981;
    color: white;
    margin-top: 0.5rem;
  }

  .btn-copy:hover {
    background: #059669;
  }

  /* Recipe Details */
  .meal-info {
    background: #f9fafb;
    padding: 1rem;
    border-radius: 6px;
    margin-bottom: 1.5rem;
  }

  .meal-info p {
    margin: 0.5rem 0;
  }

  .recipe-details h4 {
    font-size: 1.5rem;
    font-weight: 700;
    color: var(--text-primary);
    margin-bottom: 0.5rem;
  }

  .recipe-description {
    color: var(--text-secondary);
    margin-bottom: 1rem;
  }

  .recipe-meta {
    display: flex;
    gap: 1.5rem;
    margin-bottom: 1.5rem;
    padding: 1rem;
    background: #f9fafb;
    border-radius: 6px;
  }

  .recipe-meta span {
    font-size: 0.875rem;
    color: var(--text-secondary);
  }

  .ingredients-section,
  .instructions-section,
  .recipe-notes {
    margin-bottom: 1.5rem;
  }

  .ingredients-section h5,
  .instructions-section h5,
  .recipe-notes h5 {
    font-size: 1.125rem;
    font-weight: 600;
    color: var(--text-primary);
    margin-bottom: 0.75rem;
  }

  .ingredients-section ul,
  .instructions-section ol {
    margin: 0;
    padding-left: 1.5rem;
  }

  .ingredients-section li,
  .instructions-section li {
    margin-bottom: 0.5rem;
    color: var(--text-secondary);
  }

  .ingredient-notes {
    color: var(--text-tertiary);
    font-style: italic;
    font-size: 0.875rem;
  }

  .no-recipe {
    text-align: center;
    color: var(--text-tertiary);
    padding: 2rem;
  }

  @media screen and (max-width: 767px) {
    .meal-prep-container {
      padding: 1rem;
    }

    .header h2 {
      font-size: 1.5rem;
    }

    .calendar-wrapper {
      padding: 0.5rem;
    }

    .modal {
      max-width: 100%;
    }

    .modal-header,
    .modal-body,
    .modal-footer {
      padding: 1rem;
    }

    .recipe-meta {
      flex-direction: column;
      gap: 0.5rem;
    }
  }
</style>
