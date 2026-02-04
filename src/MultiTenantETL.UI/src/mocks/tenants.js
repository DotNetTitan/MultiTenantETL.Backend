// Mock data for tenants
export const mockTenants = [
  {
    id: '1',
    name: 'Acme Corporation',
    identifier: 'acme',
    description: 'A multinational company producing various products',
    contactName: 'John Smith',
    contactEmail: 'john.smith@acme.com',
    isActive: true,
    createdAt: new Date(Date.now() - 90 * 24 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString()
  },
  {
    id: '2',
    name: 'Global Industries',
    identifier: 'global',
    description: 'International industrial manufacturing company',
    contactName: 'Emily Johnson',
    contactEmail: 'emily@globalindustries.com',
    isActive: true,
    createdAt: new Date(Date.now() - 85 * 24 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 20 * 24 * 60 * 60 * 1000).toISOString()
  },
  {
    id: '3',
    name: 'Tech Solutions',
    identifier: 'techsol',
    description: 'IT solutions and consulting services',
    contactName: 'Michael Williams',
    contactEmail: 'michael@techsolutions.com',
    isActive: false,
    createdAt: new Date(Date.now() - 70 * 24 * 60 * 60 * 1000).toISOString(),
    updatedAt: new Date(Date.now() - 15 * 24 * 60 * 60 * 1000).toISOString()
  }
]
