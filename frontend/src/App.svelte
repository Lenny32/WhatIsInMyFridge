<script lang="ts">
  import { onMount } from "svelte";
  import { authStore } from "./lib/auth";
  import { getCurrentUser, logout } from "./lib/api";
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
    <header class="app-header">
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
    </header>

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

  .app-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 1rem 2rem;
    background: var(--bg-header);
    border-bottom: 1px solid var(--border-secondary);
    box-shadow: var(--shadow-header);
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

  @media screen and (max-width: 767px) {
    .app-header {
      padding: 1rem;
    }
    
    .logout-btn {
      padding: 0.4rem 1rem;
      font-size: 0.85rem;
    }
  }
</style>
