import AppShell from '../../components/layout/AppShell'
import PageHeading from '../../components/common/PageHeading'
import PasswordVerificationForm from '../../components/forms/PasswordVerificationForm'

export default function ChangePassword() {
  return <AppShell title="Change password"><div className="password-change-page">
    <PageHeading eyebrow="Account security" title="Change password" description="Confirm your current password, then verify the code sent to your registered email." />
    <section className="profile-settings-card password-change-card"><PasswordVerificationForm change /></section>
  </div></AppShell>
}
