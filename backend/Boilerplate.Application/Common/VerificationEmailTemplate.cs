namespace Boilerplate.Application.Common;

public static class VerificationEmailTemplate
{
    public static string Render(string username, string code) => $"""
        <div style="font-family: 'Segoe UI', Roboto, sans-serif; background: #313338; color: #dbdee1; padding: 32px; border-radius: 8px;">
          <h1 style="color: #f2f3f5; font-size: 20px; margin: 0 0 16px;">Confirme seu e-mail</h1>
          <p style="margin: 0 0 16px;">Olá, {username}! Use o código abaixo para confirmar seu cadastro no Boilerplate:</p>
          <p style="font-size: 32px; font-weight: 700; letter-spacing: 8px; color: #fff; background: #5865f2; padding: 16px 24px; border-radius: 8px; text-align: center; margin: 0 0 16px;">{code}</p>
          <p style="margin: 0; color: #949ba4; font-size: 13px;">Esse código expira em {VerificationCodeGenerator.ValidityMinutes} minutos. Se você não pediu isso, pode ignorar este e-mail.</p>
        </div>
        """;
}
