import AuthShell from '../../components/layout/AuthShell'
import PasswordVerificationForm from '../../components/forms/PasswordVerificationForm'

export default function ForgotPassword() {
  return <AuthShell>
    <h1 className="font-headline-lg text-headline-lg text-on-surface mb-2">Forgot your password?</h1>
    <p className="font-body-md text-body-md text-on-surface-variant mb-6">Enter the email registered with your account to request a verification code.</p>
    <PasswordVerificationForm />
  </AuthShell>
}
