export type AppNotification = {
  id: string
  recipientId: string
  type: string
  title: string
  body: string
  linkType?: string | null
  linkId?: string | null
  isRead: boolean
  createdAt: string
}
