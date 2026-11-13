import { StatusBar } from 'expo-status-bar';
import { StyleSheet, Text, View } from 'react-native';

export default function App() {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>Despacha</Text>
      <Text style={styles.subtitle}>Catálogo del cliente. Expo Go está conectado.</Text>
      <StatusBar style="light" />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#1d4e89',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 24,
  },
  title: {
    color: '#fff',
    fontSize: 36,
    fontWeight: '700',
  },
  subtitle: {
    color: '#d6e4f5',
    fontSize: 16,
    textAlign: 'center',
    marginTop: 12,
  },
});
