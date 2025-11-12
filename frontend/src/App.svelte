<script lang="ts">
  import { onMount } from "svelte";
  import { authStore } from "./lib/auth";
  import { getCurrentUser, logout } from "./lib/api";
  import Login from "./lib/Login.svelte";
  import Register from "./lib/Register.svelte";
  import Inventory from "./lib/Inventory.svelte";
  import Recipes from "./lib/Recipes.svelte";
  import GroceryList from "./lib/GroceryList.svelte";
  import Admin from "./lib/Admin.svelte";
  import Settings from "./lib/Settings.svelte";

  let authView: "login" | "register" = "login";
  let currentView: "inventory" | "recipes" | "grocery" | "admin" | "settings" = "inventory";
  let isPWA = false;
  let mobileMenuOpen = false;

  onMount(async () => {
    // Check if app is running as PWA
    isPWA = window.matchMedia('(display-mode: standalone)').matches || 
            (window.navigator as any).standalone === true;

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

  function navigateTo(view: typeof currentView) {
    currentView = view;
    mobileMenuOpen = false;
  }

  $: userName = $authStore.user?.name || "User";
  $: householdName = $authStore.household?.name || "Household";
  $: isAdmin = $authStore.user?.isAdmin || false;
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
      <button class="burger-menu" on:click={() => mobileMenuOpen = !mobileMenuOpen}>
        <span></span>
        <span></span>
        <span></span>
      </button>
      
      <div class="user-info">
        <div class="user-avatar">
          {userName.charAt(0).toUpperCase()}
        </div>
        <div class="user-details">
          <div class="user-name">{userName}</div>
          <div class="household-badge">{householdName}</div>
        </div>
      </div>
      {#if !isPWA}
        <button type="button" class="logout-btn" on:click={handleLogout}>
          Sign Out
        </button>
      {/if}
    </header>

    <nav class="main-nav" class:mobile-open={mobileMenuOpen}>
      <button 
        class="nav-btn" 
        class:active={currentView === "inventory"}
        on:click={() => navigateTo("inventory")}
      >
        Inventory
      </button>
      <button 
        class="nav-btn" 
        class:active={currentView === "recipes"}
        on:click={() => navigateTo("recipes")}
      >
        Recipes
      </button>
      <button 
        class="nav-btn" 
        class:active={currentView === "grocery"}
        on:click={() => navigateTo("grocery")}
      >
        Grocery List
      </button>
      <button 
        class="nav-btn" 
        class:active={currentView === "settings"}
        on:click={() => navigateTo("settings")}
      >
        Settings
      </button>
      {#if isAdmin}
        <button 
          class="nav-btn" 
          class:active={currentView === "admin"}
          on:click={() => navigateTo("admin")}
        >
          Admin
        </button>
      {/if}
    </nav>
    
    {#if currentView === "inventory"}
      <Inventory />
    {:else if currentView === "recipes"}
      <Recipes />
    {:else if currentView === "grocery"}
      <GroceryList />
    {:else if currentView === "settings"}
      <Settings />
    {:else if currentView === "admin"}
      <Admin />
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

  .burger-menu {
    display: none;
    flex-direction: column;
    gap: 4px;
    background: none;
    border: none;
    cursor: pointer;
    padding: 0.5rem;
  }

  .burger-menu span {
    width: 24px;
    height: 3px;
    background-color: var(--text-primary);
    border-radius: 2px;
    transition: all 0.3s ease;
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

    .burger-menu {
      display: flex;
    }
    
    .logout-btn {
      padding: 0.4rem 1rem;
      font-size: 0.85rem;
    }

    .main-nav {
      position: fixed;
      top: 0;
      left: -100%;
      width: 280px;
      height: 100vh;
      flex-direction: column;
      padding: 1rem;
      gap: 0.5rem;
      background: white;
      border-right: 1px solid #e5e7eb;
      box-shadow: 2px 0 8px rgba(0, 0, 0, 0.1);
      transition: left 0.3s ease;
      z-index: 1000;
    }

    .main-nav.mobile-open {
      left: 0;
    }

    .nav-btn {
      width: 100%;
      text-align: left;
      padding: 1rem;
      border-radius: 6px;
      border-bottom: none;
    }

    .nav-btn:hover {
      background-color: #f3f4f6;
    }

    .nav-btn.active {
      background-color: #eff6ff;
      color: #3b82f6;
      border-bottom: none;
    }
  }
</style>
