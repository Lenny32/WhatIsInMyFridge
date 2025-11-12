<script lang="ts">
  import { onMount } from "svelte";
  import { authStore } from "./auth";
  import { apiClient } from "./api";
  import type { Household } from "./types";

  let households: Household[] = [];
  let currentHousehold: Household | null = null;
  let members: Array<{ id: string; name: string; email: string; isOwner: boolean }> = [];
  let inviteEmail = "";
  let inviteError = "";
  let inviteSuccess = "";
  let isLoading = false;
  let isLoadingMembers = false;
  let switchingHousehold = false;

  $: currentUserId = $authStore.user?.id;
  $: currentHouseholdId = $authStore.household?.id;

  onMount(async () => {
    await loadHouseholds();
    if (currentHouseholdId) {
      await loadHouseholdMembers();
    }
  });

  async function loadHouseholds() {
    try {
      const response = await apiClient.GET("/api/households");
      if (response.data) {
        households = response.data as Household[];
        currentHousehold = households.find((h) => h.id === currentHouseholdId) || null;
      }
    } catch (error) {
      console.error("Failed to load households:", error);
    }
  }

  async function loadHouseholdMembers() {
    if (!currentHouseholdId) return;

    isLoadingMembers = true;
    try {
      // Get household details
      const householdResponse = await apiClient.GET("/api/households");
      if (householdResponse.data) {
        const household = (householdResponse.data as Household[]).find(
          (h) => h.id === currentHouseholdId
        );
        if (household) {
          currentHousehold = household;
          // For now, we'll just show member IDs since we don't have a user lookup endpoint
          members = household.memberIds.map((id) => ({
            id,
            name: "Member",
            email: "",
            isOwner: id === household.ownerId,
          }));
        }
      }
    } catch (error) {
      console.error("Failed to load household members:", error);
    } finally {
      isLoadingMembers = false;
    }
  }

  async function sendInvite() {
    if (!currentHouseholdId) return;

    inviteError = "";
    inviteSuccess = "";

    if (!inviteEmail || !inviteEmail.includes("@")) {
      inviteError = "Please enter a valid email address";
      return;
    }

    isLoading = true;
    try {
      const response = await apiClient.POST("/api/households/{householdId}/members", {
        params: { path: { householdId: currentHouseholdId } },
        body: { email: inviteEmail },
      });

      if (response.error) {
        throw new Error(response.error.toString());
      }

      inviteSuccess = `Invite sent to ${inviteEmail}`;
      inviteEmail = "";
      await loadHouseholdMembers();
    } catch (error: any) {
      console.error("Failed to send invite:", error);
      inviteError = error.message || "Failed to send invite. Please try again.";
    } finally {
      isLoading = false;
    }
  }

  async function switchHousehold(householdId: string) {
    switchingHousehold = true;
    try {
      const response = await apiClient.POST("/api/households/{householdId}/switch", {
        params: { path: { householdId } },
      });

      if (response.data) {
        const data = response.data as any;
        localStorage.setItem("auth_token", data.token);
        authStore.setUser($authStore.user!, data.household);
        currentHousehold = data.household;
        await loadHouseholdMembers();
      }
    } catch (error) {
      console.error("Failed to switch household:", error);
    } finally {
      switchingHousehold = false;
    }
  }

  async function removeMember(memberId: string) {
    if (!currentHouseholdId) return;
    if (!confirm("Are you sure you want to remove this member?")) return;

    try {
      const response = await apiClient.DELETE("/api/households/{householdId}/members/{memberId}", {
        params: { path: { householdId: currentHouseholdId, memberId } },
      });

      if (response.error) {
        throw new Error(response.error.toString());
      }

      await loadHouseholdMembers();
    } catch (error) {
      console.error("Failed to remove member:", error);
      alert("Failed to remove member. Please try again.");
    }
  }

  function handleKeyPress(event: KeyboardEvent) {
    if (event.key === "Enter") {
      sendInvite();
    }
  }

  $: isOwner = currentHousehold?.ownerId === currentUserId;
</script>

<div class="settings-container">
  <h1>Settings</h1>

  <section class="settings-section">
    <h2>Households</h2>
    {#if households.length > 0}
      <div class="household-list">
        {#each households as household}
          <div class="household-item" class:active={household.id === currentHouseholdId}>
            <div class="household-info">
              <div class="household-name">{household.name}</div>
              {#if household.id === currentHouseholdId}
                <span class="badge active-badge">Active</span>
              {/if}
            </div>
            {#if household.id !== currentHouseholdId}
              <button
                class="switch-btn"
                on:click={() => switchHousehold(household.id)}
                disabled={switchingHousehold}
              >
                Switch
              </button>
            {/if}
          </div>
        {/each}
      </div>
    {:else}
      <p class="empty-message">No households found</p>
    {/if}
  </section>

  {#if currentHousehold}
    <section class="settings-section">
      <h2>Household Members</h2>
      {#if isLoadingMembers}
        <p class="loading-text">Loading members...</p>
      {:else if members.length > 0}
        <div class="members-list">
          {#each members as member}
            <div class="member-item">
              <div class="member-info">
                <div class="member-avatar">
                  {member.name.charAt(0).toUpperCase()}
                </div>
                <div class="member-details">
                  <div class="member-id">{member.id}</div>
                  {#if member.isOwner}
                    <span class="badge owner-badge">Owner</span>
                  {/if}
                </div>
              </div>
              {#if isOwner && !member.isOwner}
                <button class="remove-btn" on:click={() => removeMember(member.id)}>
                  Remove
                </button>
              {/if}
            </div>
          {/each}
        </div>
      {/if}
    </section>

    {#if isOwner}
      <section class="settings-section">
        <h2>Invite Members</h2>
        <p class="section-description">
          Invite others to join your household by entering their email address. They must already
          have an account.
        </p>

        <div class="invite-form">
          <input
            type="email"
            bind:value={inviteEmail}
            placeholder="Enter email address"
            on:keypress={handleKeyPress}
            disabled={isLoading}
            class="invite-input"
          />
          <button class="invite-btn" on:click={sendInvite} disabled={isLoading || !inviteEmail}>
            {isLoading ? "Sending..." : "Send Invite"}
          </button>
        </div>

        {#if inviteError}
          <div class="error-message">{inviteError}</div>
        {/if}
        {#if inviteSuccess}
          <div class="success-message">{inviteSuccess}</div>
        {/if}
      </section>
    {/if}
  {/if}
</div>

<style>
  .settings-container {
    max-width: 800px;
    margin: 0 auto;
    padding: 2rem;
  }

  h1 {
    font-size: 2rem;
    font-weight: 700;
    color: var(--text-primary);
    margin-bottom: 2rem;
  }

  .settings-section {
    background: white;
    border-radius: 8px;
    padding: 1.5rem;
    margin-bottom: 1.5rem;
    box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  }

  h2 {
    font-size: 1.25rem;
    font-weight: 600;
    color: var(--text-primary);
    margin-bottom: 1rem;
  }

  .section-description {
    color: var(--text-secondary);
    font-size: 0.9rem;
    margin-bottom: 1rem;
  }

  .household-list,
  .members-list {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }

  .household-item,
  .member-item {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 1rem;
    border: 1px solid #e5e7eb;
    border-radius: 6px;
    transition: all 0.2s;
  }

  .household-item:hover,
  .member-item:hover {
    border-color: #d1d5db;
    box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
  }

  .household-item.active {
    border-color: #3b82f6;
    background-color: #eff6ff;
  }

  .household-info,
  .member-info {
    display: flex;
    align-items: center;
    gap: 0.75rem;
  }

  .household-name {
    font-weight: 600;
    color: var(--text-primary);
  }

  .member-avatar {
    width: 40px;
    height: 40px;
    border-radius: 50%;
    background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
    display: flex;
    align-items: center;
    justify-content: center;
    font-weight: 600;
    color: white;
  }

  .member-details {
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
  }

  .member-id {
    font-size: 0.85rem;
    color: var(--text-secondary);
    font-family: monospace;
  }

  .badge {
    display: inline-block;
    padding: 0.125rem 0.5rem;
    border-radius: 4px;
    font-size: 0.75rem;
    font-weight: 600;
  }

  .active-badge {
    background-color: #dbeafe;
    color: #1e40af;
  }

  .owner-badge {
    background-color: #fef3c7;
    color: #92400e;
  }

  .switch-btn,
  .remove-btn {
    padding: 0.5rem 1rem;
    border-radius: 6px;
    font-size: 0.875rem;
    font-weight: 500;
    cursor: pointer;
    transition: all 0.2s;
    border: none;
  }

  .switch-btn {
    background-color: #3b82f6;
    color: white;
  }

  .switch-btn:hover:not(:disabled) {
    background-color: #2563eb;
  }

  .switch-btn:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  .remove-btn {
    background-color: #fee2e2;
    color: #dc2626;
  }

  .remove-btn:hover {
    background-color: #fecaca;
  }

  .invite-form {
    display: flex;
    gap: 0.75rem;
    margin-bottom: 1rem;
  }

  .invite-input {
    flex: 1;
    padding: 0.75rem 1rem;
    border: 1px solid #d1d5db;
    border-radius: 6px;
    font-size: 1rem;
    transition: border-color 0.2s;
  }

  .invite-input:focus {
    outline: none;
    border-color: #3b82f6;
    box-shadow: 0 0 0 3px rgba(59, 130, 246, 0.1);
  }

  .invite-btn {
    padding: 0.75rem 1.5rem;
    background-color: #3b82f6;
    color: white;
    border: none;
    border-radius: 6px;
    font-size: 1rem;
    font-weight: 500;
    cursor: pointer;
    transition: background-color 0.2s;
    white-space: nowrap;
  }

  .invite-btn:hover:not(:disabled) {
    background-color: #2563eb;
  }

  .invite-btn:disabled {
    opacity: 0.5;
    cursor: not-allowed;
  }

  .error-message,
  .success-message {
    padding: 0.75rem 1rem;
    border-radius: 6px;
    font-size: 0.9rem;
  }

  .error-message {
    background-color: #fee2e2;
    color: #dc2626;
    border: 1px solid #fecaca;
  }

  .success-message {
    background-color: #d1fae5;
    color: #065f46;
    border: 1px solid #a7f3d0;
  }

  .empty-message,
  .loading-text {
    color: var(--text-secondary);
    font-style: italic;
  }

  @media screen and (max-width: 767px) {
    .settings-container {
      padding: 1rem;
    }

    h1 {
      font-size: 1.5rem;
    }

    .invite-form {
      flex-direction: column;
    }

    .member-id {
      font-size: 0.7rem;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 150px;
    }
  }
</style>
