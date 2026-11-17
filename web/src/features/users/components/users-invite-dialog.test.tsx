import { useState } from 'react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { render } from 'vitest-browser-react'
import { userEvent } from 'vitest/browser'
import { showSubmittedData } from '@/lib/show-submitted-data'
import { UsersInviteDialog } from './users-invite-dialog'

vi.mock('@/lib/show-submitted-data', () => ({ showSubmittedData: vi.fn() }))

describe('UsersInviteDialog', () => {
  beforeEach(() => vi.clearAllMocks())

  it('renders the dialog title and description', async () => {
    const { getByRole, getByText } = await render(
      <UsersInviteDialog open onOpenChange={vi.fn()} />
    )

    const title = getByRole('heading', {
      level: 2,
      name: /(Invitar Usuario|Invite User)/i,
    })
    const desc = getByText(/(Invita a un nuevo usuario|Invite new user)/i)

    await expect.element(title).toBeInTheDocument()
    await expect.element(desc).toBeInTheDocument()
  })

  it('closes when Cancel is clicked', async () => {
    const onOpenChange = vi.fn()
    const { getByRole } = await render(
      <UsersInviteDialog open onOpenChange={onOpenChange} />
    )

    const cancelButton = getByRole('button', { name: /(Cancelar|Cancel)/i })
    await userEvent.click(cancelButton)

    expect(onOpenChange).toHaveBeenCalledOnce()
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('shows error messages when submitting empty form, and clears them as fields are filled', async () => {
    const onOpenChange = vi.fn()
    const { getByRole, getByText } = await render(
      <UsersInviteDialog open onOpenChange={onOpenChange} />
    )

    const submitButton = getByRole('button', { name: /(Enviar invitación|Invitar|Invite)/i })
    await userEvent.click(submitButton)

    const emailErrorMessage = getByText(/(Introduce un correo electrónico para invitar\.|Please enter an email)/i)
    const roleErrorMessage = getByText(/(El rol es obligatorio\.|Role is required)/i)

    await expect.element(emailErrorMessage).toBeInTheDocument()
    await expect.element(roleErrorMessage).toBeInTheDocument()

    const emailInput = getByRole('textbox', { name: /(Correo electrónico|Email)/i })
    await userEvent.fill(emailInput, 'test@example.com')

    const roleSelect = getByRole('combobox', { name: /(Rol|Role)/i })
    await userEvent.click(roleSelect)
    await userEvent.click(getByRole('option', { name: /(Administrador|Admin)/i }))

    await expect.element(emailErrorMessage).not.toBeInTheDocument()
    await expect.element(roleErrorMessage).not.toBeInTheDocument()
  })

  it('resets entered values when the dialog is closed and reopened', async () => {
    function Harness() {
      const [open, setOpen] = useState(true)
      return (
        <>
          <button type='button' onClick={() => setOpen(true)}>
            Reopen
          </button>
          <UsersInviteDialog open={open} onOpenChange={setOpen} />
        </>
      )
    }

    const { getByRole } = await render(<Harness />)

    const EMAIL_VALUE = 'test@example.com'

    const emailInput = getByRole('textbox', { name: /(Correo electrónico|Email)/i })
    await userEvent.fill(emailInput, EMAIL_VALUE)
    await expect.element(emailInput).toHaveValue(EMAIL_VALUE)

    const cancelButton = getByRole('button', { name: /(Cancelar|Cancel)/i })
    await userEvent.click(cancelButton)

    const reopenButton = getByRole('button', { name: /Reopen/i })
    await userEvent.click(reopenButton)
    await expect.element(emailInput).toHaveValue('')
  })

  it('shows submitted data when the form is submitted successfully', async () => {
    const onOpenChange = vi.fn()
    const { getByRole } = await render(
      <UsersInviteDialog open onOpenChange={onOpenChange} />
    )

    const EMAIL_VALUE = 'test@example.com'
    const DESC_VALUE = 'This is a test description'

    const emailInput = getByRole('textbox', { name: /(Correo electrónico|Email)/i })
    await userEvent.fill(emailInput, EMAIL_VALUE)

    const roleSelect = getByRole('combobox', { name: /(Rol|Role)/i })
    await userEvent.click(roleSelect)
    await userEvent.click(getByRole('option', { name: /(Administrador|Admin)/i }))

    const descInput = getByRole('textbox', { name: /(Descripción|Description)/i })
    await userEvent.fill(descInput, DESC_VALUE)

    const submitButton = getByRole('button', { name: /(Enviar invitación|Invitar|Invite)/i })
    await userEvent.click(submitButton)

    expect(showSubmittedData).toHaveBeenCalledOnce()
    expect(showSubmittedData).toHaveBeenCalledWith({
      email: EMAIL_VALUE,
      role: 'admin',
      desc: DESC_VALUE,
    })
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })
})
