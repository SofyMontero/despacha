import { StatusBar } from 'expo-status-bar'
import { useEffect, useMemo, useState } from 'react'
import {
  ActivityIndicator,
  FlatList,
  Image,
  Pressable,
  SafeAreaView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native'

const API = 'http://192.168.1.6:5080'
const STORE = `${API}/api/store/multilicores`
const FALLBACK = 'https://multilicoreschapinero.com/sistema/assets/img/logoM.png'

type Screen = 'home' | 'products' | 'promos' | 'cart' | 'checkout' | 'done'

type Category = { id: number; name: string; imageUrl: string | null }
type Product = {
  id: number
  name: string
  unitPrice: number
  packPrice: number
  sellsByUnit: boolean
  imageUrl: string | null
}
type CartItem = { id: number; name: string; kind: 'unidad' | 'paca'; quantity: number; unitPrice: number }

function money(value: number) {
  return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', maximumFractionDigits: 0 }).format(value)
}

async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path)
  if (!response.ok) {
    throw new Error(`No se pudo cargar (${response.status})`)
  }
  return response.json() as Promise<T>
}

export default function App() {
  const [screen, setScreen] = useState<Screen>('home')
  const [category, setCategory] = useState<Category | null>(null)
  const [cart, setCart] = useState<CartItem[]>([])
  const [orderNumber, setOrderNumber] = useState('')
  const count = cart.reduce((sum, item) => sum + item.quantity, 0)

  function add(product: Product, kind: 'unidad' | 'paca') {
    const unitPrice = kind === 'paca' ? product.packPrice : product.unitPrice
    setCart((current) => {
      const index = current.findIndex((item) => item.id === product.id && item.kind === kind)
      if (index >= 0) {
        const next = [...current]
        next[index] = { ...next[index], quantity: next[index].quantity + 1 }
        return next
      }
      return [...current, { id: product.id, name: product.name, kind, quantity: 1, unitPrice }]
    })
  }

  return (
    <SafeAreaView style={styles.screen}>
      <StatusBar style="light" />
      <View style={styles.header}>
        <Text style={styles.brand}>Despacha</Text>
        <Pressable onPress={() => setScreen('cart')}>
          <Text style={styles.cartLink}>Carrito ({count})</Text>
        </Pressable>
      </View>
      {screen === 'home' ? <Home onOpen={(item) => { setCategory(item); setScreen('products') }} onPromos={() => setScreen('promos')} /> : null}
      {screen === 'products' && category ? (
        <Products category={category} onBack={() => setScreen('home')} onAdd={add} />
      ) : null}
      {screen === 'promos' ? <Promos onBack={() => setScreen('home')} onAdd={add} /> : null}
      {screen === 'cart' ? (
        <Cart
          items={cart}
          onBack={() => setScreen('home')}
          onChange={setCart}
          onCheckout={() => setScreen('checkout')}
        />
      ) : null}
      {screen === 'checkout' ? (
        <Checkout
          items={cart}
          onBack={() => setScreen('cart')}
          onDone={(number) => { setCart([]); setOrderNumber(number); setScreen('done') }}
        />
      ) : null}
      {screen === 'done' ? <Done number={orderNumber} onHome={() => setScreen('home')} /> : null}
    </SafeAreaView>
  )
}

function Home({ onOpen, onPromos }: { onOpen: (category: Category) => void; onPromos: () => void }) {
  const [items, setItems] = useState<Category[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getJson<Category[]>(`${STORE}/categories`)
      .then(setItems)
      .catch((reason: Error) => setError(reason.message))
      .finally(() => setLoading(false))
  }, [])

  return (
    <View style={styles.body}>
      <Text style={styles.title}>Elige tu categoría</Text>
      <Pressable style={styles.banner} onPress={onPromos}><Text style={styles.bannerText}>Ver promociones</Text></Pressable>
      {loading ? <ActivityIndicator color="#1d4e89" /> : null}
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <FlatList
        data={items}
        keyExtractor={(item) => String(item.id)}
        renderItem={({ item }) => (
          <Pressable style={styles.card} onPress={() => onOpen(item)}>
            <Image source={{ uri: item.imageUrl || FALLBACK }} style={styles.thumb} />
            <Text style={styles.cardTitle}>{item.name}</Text>
          </Pressable>
        )}
      />
    </View>
  )
}

function Products({ category, onBack, onAdd }: { category: Category; onBack: () => void; onAdd: (product: Product, kind: 'unidad' | 'paca') => void }) {
  const [items, setItems] = useState<Product[]>([])
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    getJson<Product[]>(`${STORE}/products?categoryId=${category.id}`)
      .then(setItems)
      .catch((reason: Error) => setError(reason.message))
      .finally(() => setLoading(false))
  }, [category.id])

  return (
    <View style={styles.body}>
      <Pressable onPress={onBack}><Text style={styles.link}>← Categorías</Text></Pressable>
      <Text style={styles.title}>{category.name}</Text>
      {loading ? <ActivityIndicator color="#1d4e89" /> : null}
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <FlatList data={items} keyExtractor={(item) => String(item.id)} renderItem={({ item }) => <ProductRow product={item} onAdd={onAdd} />} />
    </View>
  )
}

function Promos({ onBack, onAdd }: { onBack: () => void; onAdd: (product: Product, kind: 'unidad' | 'paca') => void }) {
  const [items, setItems] = useState<Product[]>([])
  const [error, setError] = useState('')

  useEffect(() => {
    getJson<Array<Product & { title: string }>>(`${STORE}/promotions`)
      .then((rows) => setItems(rows.map((row) => ({ ...row, name: row.title || row.name }))))
      .catch((reason: Error) => setError(reason.message))
  }, [])

  return (
    <View style={styles.body}>
      <Pressable onPress={onBack}><Text style={styles.link}>← Categorías</Text></Pressable>
      <Text style={styles.title}>Promociones</Text>
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <FlatList data={items} keyExtractor={(item) => String(item.id)} renderItem={({ item }) => <ProductRow product={item} onAdd={onAdd} />} />
    </View>
  )
}

function ProductRow({ product, onAdd }: { product: Product; onAdd: (product: Product, kind: 'unidad' | 'paca') => void }) {
  return (
    <View style={styles.product}>
      <Text style={styles.cardTitle}>{product.name}</Text>
      <Text>{product.sellsByUnit ? `Unidad ${money(product.unitPrice)}` : ''}  Paca {money(product.packPrice)}</Text>
      <View style={styles.row}>
        {product.sellsByUnit ? <Pressable style={styles.button} onPress={() => onAdd(product, 'unidad')}><Text style={styles.buttonText}>Unidad</Text></Pressable> : null}
        <Pressable style={styles.button} onPress={() => onAdd(product, 'paca')}><Text style={styles.buttonText}>Paca</Text></Pressable>
      </View>
    </View>
  )
}

function Cart({ items, onBack, onChange, onCheckout }: { items: CartItem[]; onBack: () => void; onChange: (items: CartItem[]) => void; onCheckout: () => void }) {
  const total = useMemo(() => items.reduce((sum, item) => sum + item.unitPrice * item.quantity, 0), [items])
  return (
    <View style={styles.body}>
      <Pressable onPress={onBack}><Text style={styles.link}>← Seguir comprando</Text></Pressable>
      <Text style={styles.title}>Carrito</Text>
      <FlatList
        data={items}
        keyExtractor={(item, index) => `${item.id}-${item.kind}-${index}`}
        ListEmptyComponent={<Text>El carrito está vacío.</Text>}
        renderItem={({ item, index }) => (
          <View style={styles.product}>
            <Text style={styles.cardTitle}>{item.name}</Text>
            <Text>{item.kind} × {item.quantity} · {money(item.unitPrice * item.quantity)}</Text>
            <Pressable onPress={() => onChange(items.filter((_, itemIndex) => itemIndex !== index))}><Text style={styles.link}>Quitar</Text></Pressable>
          </View>
        )}
      />
      <Text style={styles.total}>Total {money(total)}</Text>
      {items.length > 0 ? <Pressable style={styles.button} onPress={onCheckout}><Text style={styles.buttonText}>Hacer pedido</Text></Pressable> : null}
    </View>
  )
}

function Checkout({ items, onBack, onDone }: { items: CartItem[]; onBack: () => void; onDone: (number: string) => void }) {
  const [phone, setPhone] = useState('')
  const [name, setName] = useState('')
  const [address, setAddress] = useState('')
  const [zone, setZone] = useState('')
  const [note, setNote] = useState('')
  const [error, setError] = useState('')
  const [needsRegister, setNeedsRegister] = useState(false)
  const [sending, setSending] = useState(false)

  async function send() {
    setSending(true)
    setError('')
    try {
      if (needsRegister) {
        const created = await fetch(`${STORE}/customers`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ name, phone, address, zone }),
        })
        if (!created.ok) {
          const problem = await created.json()
          throw new Error(problem.errors ? Object.values(problem.errors).flat().join(' ') : 'No se pudo registrar')
        }
      }
      const response = await fetch(`${STORE}/orders`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          phone,
          note,
          lines: items.map((item) => ({ productId: item.id, kind: item.kind, quantity: item.quantity })),
        }),
      })
      const body = await response.json()
      if (response.status === 400 && JSON.stringify(body).includes('no está registrado')) {
        setNeedsRegister(true)
        return
      }
      if (!response.ok) {
        throw new Error(body.errors ? Object.values(body.errors).flat().join(' ') : 'No se pudo crear el pedido')
      }
      onDone(body.number)
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Error al enviar')
    } finally {
      setSending(false)
    }
  }

  return (
    <View style={styles.body}>
      <Pressable onPress={onBack}><Text style={styles.link}>← Carrito</Text></Pressable>
      <Text style={styles.title}>Tu pedido</Text>
      <TextInput style={styles.input} placeholder="Celular" keyboardType="phone-pad" value={phone} onChangeText={setPhone} />
      {needsRegister ? (
        <>
          <Text>Ese teléfono no está registrado.</Text>
          <TextInput style={styles.input} placeholder="Nombre" value={name} onChangeText={setName} />
          <TextInput style={styles.input} placeholder="Dirección" value={address} onChangeText={setAddress} />
          <TextInput style={styles.input} placeholder="Zona" value={zone} onChangeText={setZone} />
        </>
      ) : null}
      <TextInput style={styles.input} placeholder="Nota" value={note} onChangeText={setNote} />
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <Pressable style={styles.button} disabled={sending} onPress={send}>
        <Text style={styles.buttonText}>{sending ? 'Enviando…' : 'Enviar pedido'}</Text>
      </Pressable>
    </View>
  )
}

function Done({ number, onHome }: { number: string; onHome: () => void }) {
  return (
    <View style={styles.done}>
      <Text style={styles.title}>Pedido enviado</Text>
      <Text style={styles.total}>{number}</Text>
      <Text>Quedó pendiente. El administrador lo ve en el panel.</Text>
      <Pressable style={styles.button} onPress={onHome}><Text style={styles.buttonText}>Volver al catálogo</Text></Pressable>
    </View>
  )
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: '#f4f7fb' },
  header: { backgroundColor: '#1d4e89', padding: 16, flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  brand: { color: '#fff', fontSize: 22, fontWeight: '700' },
  cartLink: { color: '#fff', fontSize: 16 },
  body: { flex: 1, padding: 16, gap: 8 },
  title: { fontSize: 24, fontWeight: '700', color: '#16324f' },
  banner: { backgroundColor: '#e7f0fa', padding: 12, borderRadius: 8 },
  bannerText: { color: '#1d4e89', fontWeight: '700' },
  card: { backgroundColor: '#fff', borderRadius: 8, marginBottom: 10, overflow: 'hidden' },
  thumb: { height: 120, backgroundColor: '#d9e3ef' },
  cardTitle: { fontSize: 16, fontWeight: '700', padding: 8, color: '#16324f' },
  product: { backgroundColor: '#fff', borderRadius: 8, padding: 12, marginBottom: 10, gap: 6 },
  row: { flexDirection: 'row', gap: 8 },
  button: { backgroundColor: '#1d4e89', borderRadius: 8, paddingVertical: 10, paddingHorizontal: 14, alignItems: 'center' },
  buttonText: { color: '#fff', fontWeight: '700' },
  link: { color: '#1d4e89', fontWeight: '700' },
  error: { color: '#9b2335' },
  total: { fontSize: 18, fontWeight: '700' },
  input: { backgroundColor: '#fff', borderRadius: 8, padding: 12, borderWidth: 1, borderColor: '#d5deea' },
  done: { flex: 1, padding: 24, gap: 12, justifyContent: 'center' },
})
