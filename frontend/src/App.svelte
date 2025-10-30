<script lang="ts">
  import { onMount } from "svelte";
  import { authStore } from "./lib/auth";
  import { getCurrentUser } from "./lib/api";
  import Login from "./lib/Login.svelte";
  import Register from "./lib/Register.svelte";
  import Inventory from "./lib/Inventory.svelte";
  import Recipes from "./lib/Recipes.svelte";

  let authView: "login" | "register" = "login";
  let currentView: "inventory" | "recipes" = "inventory";

  onMount(async () => {
    const token = localStorage.getItem("auth_token");
    if (!token) {
      console.log("No token found in localStorage");
      authStore.setLoading(false);
      return;
    }
    
    try {
      console.log("Token found, fetching current user...");
      const data = await getCurrentUser();
      authStore.setUser(data.user, data.household);
      console.log("User authenticated successfully");
    } catch (error) {
      console.error("Failed to restore session:", error);
      authStore.setLoading(false);
    }
  });
</script>

{#if $authStore.isLoading}
  <div class="loading-screen">
    <p>Loading...</p>
  </div>
{:else if !$authStore.isAuthenticated}
  {#if authView === "login"}
    <Login onSwitchToRegister={() => authView = "register"} />
  {:else}
    <Register onSwitchToLogin={() => authView = "login"} />
  {/if}
{:else}
  <div class="app-container">
    <nav class="main-nav">
      <button 
        class="nav-btn" 
        class:active={currentView === "inventory"}
        on:click={() => currentView = "inventory"}
      >
        Inventory
      </button>
      <button 
        class="nav-btn" 
        class:active={currentView === "recipes"}
        on:click={() => currentView = "recipes"}
      >
        Recipes
      </button>
    </nav>
    
    {#if currentView === "inventory"}
      <Inventory />
    {:else}
      <Recipes />
    {/if}
  </div>
{/if}

<style>
  .loading-screen {
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 100vh;
    font-size: 1.25rem;
    color: var(--text-tertiary);
    background-color: var(--bg-primary);
  }

  .app-container {
    min-height: 100vh;
    background-color: var(--bg-primary);
  }

  .main-nav {
    background: white;
    border-bottom: 1px solid #e5e7eb;
    padding: 0 2rem;
    display: flex;
    gap: 1rem;
  }

  .nav-btn {
    background: none;
    border: none;
    padding: 1rem 1.5rem;
    font-size: 1rem;
    font-weight: 500;
    color: #6b7280;
    cursor: pointer;
    border-bottom: 2px solid transparent;
    transition: all 0.2s;
  }

  .nav-btn:hover {
    color: #374151;
  }

  .nav-btn.active {
    color: #3b82f6;
    border-bottom-color: #3b82f6;
  }
</style>
