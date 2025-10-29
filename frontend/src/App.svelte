<script lang="ts">
  import { onMount } from "svelte";
  import { authStore } from "./lib/auth";
  import { getCurrentUser } from "./lib/api";
  import Login from "./lib/Login.svelte";
  import Register from "./lib/Register.svelte";
  import Inventory from "./lib/Inventory.svelte";

  let authView: "login" | "register" = "login";

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
  <Inventory />
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
</style>
