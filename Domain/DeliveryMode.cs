namespace CallbackListener.Domain;

public enum DeliveryMode
{
    WebOnly = 0,   // Show in dashboard only, never forward to agent
    Local   = 1,   // Forward to local agent only
    Both    = 2,   // Forward to agent AND show in dashboard
}
