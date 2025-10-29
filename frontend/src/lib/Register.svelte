<script lang="ts">
  import { register } from "./api";
  import { authStore, setToken } from "./auth";

  export let onSwitchToLogin: () => void;

  let email = "";
  let password = "";
  let name = "";
  let householdName = "";
  let error = "";
  let loading = false;

  async function handleSubmit() {
    error = "";
    loading = true;

    try {
      const response = await register({ email, password, name, householdName });
      setToken(response.token);
      authStore.setUser(response.user, response.household);
    } catch (err) {
      console.error("Registration failed:", err);
      error = "Registration failed. Email may already be in use.";
    } finally {
      loading = false;
    }
  }
</script>

<div class="auth-container">
  <div class="auth-card">
    <h1>Create Account</h1>
    <p>Start managing your food inventory today</p>

    {#if error}
      <div class="error-message">{error}</div>
    {/if}

    <form on:submit|preventDefault={handleSubmit}>
      <div>
        <label for="name">Your Name</label>
        <input
          id="name"
          type="text"
          bind:value={name}
          required
          placeholder="John Doe"
          autocomplete="name"
        />
      </div>

      <div>
        <label for="email">Email</label>
        <input
          id="email"
          type="email"
          bind:value={email}
          required
          placeholder="you@example.com"
          autocomplete="email"
        />
      </div>

      <div>
        <label for="password">Password</label>
        <input
          id="password"
          type="password"
          bind:value={password}
          required
          placeholder="At least 6 characters"
          autocomplete="new-password"
          minlength="6"
        />
      </div>

      <div>
        <label for="householdName">Household Name</label>
        <input
          id="householdName"
          type="text"
          bind:value={householdName}
          required
          placeholder="Smith Family"
        />
      </div>

      <button type="submit" disabled={loading || !email || !password || !name || !householdName}>
        {loading ? "Creating account..." : "Create Account"}
      </button>
    </form>

    <p class="auth-switch">
      Already have an account?
      <button type="button" class="link-button" on:click={onSwitchToLogin}>
        Sign in here
      </button>
    </p>
  </div>
</div>

<style>
  .auth-container {
    display: flex;
    align-items: center;
    justify-content: center;
    min-height: 100vh;
    padding: 1rem;
    background-color: var(--bg-primary);
  }

  .auth-card {
    background: var(--bg-card);
    border-radius: 12px;
    box-shadow: var(--shadow-card);
    border: 1px solid var(--border-secondary);
    padding: 2rem;
    max-width: 400px;
    width: 100%;
  }

  .auth-card h1 {
    margin-top: 0;
    margin-bottom: 0.5rem;
    color: var(--text-primary);
  }

  .auth-card > p {
    margin-top: 0;
    margin-bottom: 1.5rem;
    color: var(--text-tertiary);
  }

  .error-message {
    background: var(--status-expired-bg);
    border: 1px solid var(--status-expired);
    color: var(--status-expired);
    padding: 0.75rem;
    border-radius: 6px;
    margin-bottom: 1rem;
    font-size: 0.9rem;
  }

  form {
    display: flex;
    flex-direction: column;
    gap: 1rem;
  }

  form > div {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  label {
    font-weight: 500;
    color: var(--text-secondary);
    font-size: 0.9rem;
  }

  input {
    padding: 0.75rem;
    border: 1px solid var(--border-input);
    border-radius: 6px;
    font-size: 1rem;
    background-color: var(--bg-input);
    color: var(--text-primary);
    transition: border-color 0.2s ease;
  }

  input:focus {
    outline: none;
    border-color: var(--bg-button);
  }

  button[type="submit"] {
    margin-top: 0.5rem;
    padding: 0.75rem;
    background: var(--bg-button);
    color: var(--text-button);
    border: none;
    border-radius: 6px;
    font-size: 1rem;
    font-weight: 500;
    cursor: pointer;
    transition: all 0.2s ease;
  }

  button[type="submit"]:hover:not(:disabled) {
    filter: brightness(1.1);
  }

  button[type="submit"]:disabled {
    opacity: 0.6;
    cursor: not-allowed;
  }

  .auth-switch {
    margin-top: 1.5rem;
    text-align: center;
    color: var(--text-tertiary);
    font-size: 0.9rem;
  }

  .link-button {
    background: none;
    border: none;
    color: var(--bg-button);
    text-decoration: underline;
    cursor: pointer;
    font-size: inherit;
    padding: 0;
    font-weight: 500;
  }

  .link-button:hover {
    filter: brightness(1.2);
  }
  
  @media screen and (max-width: 767px) {
    .auth-card {
      padding: 1.5rem;
    }
  }
</style>
