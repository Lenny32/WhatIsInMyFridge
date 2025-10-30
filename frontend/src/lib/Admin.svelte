<script lang="ts">
  import { onMount } from "svelte";
  import { getToken } from "./auth";

  interface AdminUser {
    id: string;
    email: string;
    name: string;
    isAdmin: boolean;
    createdAt: string;
    updatedAt: string;
  }

  let users: AdminUser[] = [];
  let loading = true;
  let error = "";
  let selectedUser: AdminUser | null = null;
  let newPassword = "";
  let resetting = false;
  let successMessage = "";

  async function loadUsers() {
    try {
      loading = true;
      error = "";
      const token = getToken();
      const response = await fetch("/api/admin/users", {
        headers: {
          Authorization: `Bearer ${token}`,
        },
      });

      if (!response.ok) {
        if (response.status === 403) {
          throw new Error("You do not have admin permissions");
        }
        throw new Error("Failed to load users");
      }

      users = await response.json();
    } catch (e) {
      error = e instanceof Error ? e.message : "Failed to load users";
    } finally {
      loading = false;
    }
  }

  async function resetPassword() {
    if (!selectedUser || !newPassword) return;

    try {
      resetting = true;
      error = "";
      successMessage = "";

      const token = getToken();
      const response = await fetch(
        `/api/admin/users/${selectedUser.id}/reset-password`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Authorization: `Bearer ${token}`,
          },
          body: JSON.stringify({ newPassword }),
        },
      );

      if (!response.ok) {
        throw new Error("Failed to reset password");
      }

      successMessage = `Password reset successfully for ${selectedUser.email}`;
      selectedUser = null;
      newPassword = "";
    } catch (e) {
      error = e instanceof Error ? e.message : "Failed to reset password";
    } finally {
      resetting = false;
    }
  }

  function formatDate(dateString: string) {
    const date = new Date(dateString);
    return date.toLocaleDateString() + " " + date.toLocaleTimeString();
  }

  onMount(() => {
    loadUsers();
  });
</script>

<div class="admin-container">
  <div class="admin-header">
    <h1>User Management</h1>
    <p class="admin-subtitle">View and manage user accounts</p>
  </div>

  {#if error}
    <div class="error-banner">{error}</div>
  {/if}

  {#if successMessage}
    <div class="success-banner">{successMessage}</div>
  {/if}

  {#if loading}
    <div class="loading">Loading users...</div>
  {:else}
    <div class="users-table-container">
      <table class="users-table">
        <thead>
          <tr>
            <th>Email</th>
            <th>Name</th>
            <th>Admin</th>
            <th>Created At</th>
            <th>Actions</th>
          </tr>
        </thead>
        <tbody>
          {#each users as user}
            <tr>
              <td>{user.email}</td>
              <td>{user.name}</td>
              <td>
                {#if user.isAdmin}
                  <span class="badge badge-admin">Admin</span>
                {:else}
                  <span class="badge badge-user">User</span>
                {/if}
              </td>
              <td class="date-cell">{formatDate(user.createdAt)}</td>
              <td>
                <button
                  type="button"
                  class="btn-action"
                  on:click={() => {
                    selectedUser = user;
                    newPassword = "";
                    successMessage = "";
                  }}
                >
                  Reset Password
                </button>
              </td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
</div>

{#if selectedUser}
  <div class="modal-backdrop" on:click={() => (selectedUser = null)}>
    <div class="modal" on:click|stopPropagation>
      <div class="modal-header">
        <h2>Reset Password</h2>
        <button
          type="button"
          class="close-btn"
          on:click={() => (selectedUser = null)}
        >
          ×
        </button>
      </div>
      <div class="modal-body">
        <p class="user-info">
          Resetting password for <strong>{selectedUser.email}</strong>
        </p>
        <form on:submit|preventDefault={resetPassword}>
          <div class="form-group">
            <label for="newPassword">New Password</label>
            <input
              id="newPassword"
              type="password"
              bind:value={newPassword}
              placeholder="Enter new password (min 6 characters)"
              minlength="6"
              required
            />
          </div>
          <div class="modal-actions">
            <button
              type="button"
              class="btn btn-secondary"
              on:click={() => (selectedUser = null)}
            >
              Cancel
            </button>
            <button
              type="submit"
              class="btn btn-primary"
              disabled={resetting || newPassword.length < 6}
            >
              {resetting ? "Resetting..." : "Reset Password"}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
{/if}

<style>
  .admin-container {
    max-width: 1200px;
    margin: 0 auto;
    padding: 2rem;
  }

  .admin-header {
    margin-bottom: 2rem;
  }

  .admin-header h1 {
    font-size: 2rem;
    font-weight: 700;
    color: #111827;
    margin: 0 0 0.5rem 0;
  }

  .admin-subtitle {
    color: #6b7280;
    margin: 0;
  }

  .error-banner {
    background-color: #fee2e2;
    color: #991b1b;
    padding: 1rem;
    border-radius: 8px;
    margin-bottom: 1rem;
  }

  .success-banner {
    background-color: #d1fae5;
    color: #065f46;
    padding: 1rem;
    border-radius: 8px;
    margin-bottom: 1rem;
  }

  .loading {
    text-align: center;
    padding: 3rem;
    color: #6b7280;
  }

  .users-table-container {
    background: white;
    border-radius: 8px;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
    overflow: hidden;
  }

  .users-table {
    width: 100%;
    border-collapse: collapse;
  }

  .users-table thead {
    background-color: #f9fafb;
    border-bottom: 1px solid #e5e7eb;
  }

  .users-table th {
    text-align: left;
    padding: 0.75rem 1rem;
    font-size: 0.875rem;
    font-weight: 600;
    color: #374151;
    text-transform: uppercase;
    letter-spacing: 0.05em;
  }

  .users-table td {
    padding: 1rem;
    border-bottom: 1px solid #e5e7eb;
    color: #111827;
  }

  .users-table tbody tr:hover {
    background-color: #f9fafb;
  }

  .date-cell {
    font-size: 0.875rem;
    color: #6b7280;
  }

  .badge {
    display: inline-block;
    padding: 0.25rem 0.5rem;
    border-radius: 4px;
    font-size: 0.75rem;
    font-weight: 600;
  }

  .badge-admin {
    background-color: #dbeafe;
    color: #1e40af;
  }

  .badge-user {
    background-color: #e5e7eb;
    color: #374151;
  }

  .btn-action {
    background-color: #3b82f6;
    color: white;
    border: none;
    padding: 0.5rem 1rem;
    border-radius: 6px;
    font-size: 0.875rem;
    font-weight: 500;
    cursor: pointer;
    transition: background-color 0.2s;
  }

  .btn-action:hover {
    background-color: #2563eb;
  }

  .modal-backdrop {
    position: fixed;
    top: 0;
    left: 0;
    width: 100%;
    height: 100%;
    background-color: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1000;
  }

  .modal {
    background: white;
    border-radius: 12px;
    width: 90%;
    max-width: 500px;
    box-shadow: 0 20px 25px -5px rgba(0, 0, 0, 0.1);
  }

  .modal-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 1.5rem;
    border-bottom: 1px solid #e5e7eb;
  }

  .modal-header h2 {
    font-size: 1.5rem;
    font-weight: 700;
    color: #111827;
    margin: 0;
  }

  .close-btn {
    background: none;
    border: none;
    font-size: 2rem;
    color: #6b7280;
    cursor: pointer;
    line-height: 1;
    padding: 0;
    width: 2rem;
    height: 2rem;
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .close-btn:hover {
    color: #111827;
  }

  .modal-body {
    padding: 1.5rem;
  }

  .user-info {
    margin-bottom: 1.5rem;
    color: #374151;
  }

  .form-group {
    margin-bottom: 1.5rem;
  }

  .form-group label {
    display: block;
    font-weight: 500;
    color: #374151;
    margin-bottom: 0.5rem;
  }

  .form-group input {
    width: 100%;
    padding: 0.75rem;
    border: 1px solid #d1d5db;
    border-radius: 6px;
    font-size: 1rem;
    box-sizing: border-box;
  }

  .form-group input:focus {
    outline: none;
    border-color: #3b82f6;
    box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
  }

  .modal-actions {
    display: flex;
    gap: 0.75rem;
    justify-content: flex-end;
  }

  .btn {
    padding: 0.75rem 1.5rem;
    border-radius: 6px;
    font-size: 1rem;
    font-weight: 500;
    cursor: pointer;
    border: none;
    transition: all 0.2s;
  }

  .btn-secondary {
    background-color: white;
    color: #374151;
    border: 1px solid #d1d5db;
  }

  .btn-secondary:hover {
    background-color: #f9fafb;
  }

  .btn-primary {
    background-color: #3b82f6;
    color: white;
  }

  .btn-primary:hover:not(:disabled) {
    background-color: #2563eb;
  }

  .btn-primary:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  @media screen and (max-width: 767px) {
    .admin-container {
      padding: 1rem;
    }

    .users-table-container {
      overflow-x: auto;
    }

    .users-table {
      font-size: 0.875rem;
    }

    .users-table th,
    .users-table td {
      padding: 0.5rem;
    }

    .modal {
      width: 95%;
    }
  }
</style>
