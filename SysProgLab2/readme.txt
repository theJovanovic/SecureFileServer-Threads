Napomene za izradu domaćeg zadatka:
	Web server implementirati kao konzolnu aplikaciju koja loguje sve primljene zahteve i informacije 
	o njihovoj obradi (da li je došlo do greške, da li je zahtev uspešno obrađen i ostale ključe detalje). 
	Web server treba da kešira u memoriji odgovore na primljene zahteve, tako da u slučaju da stigne 
	isti zahtev, prosleđuje se već pripremljeni odgovor. Kao klijentsku aplikaciju možete koristiti Web 
	browser ili možete po potrebi kreirati zasebnu konzolnu aplikaciju. Za realizaciju koristiti funkcije 
	iz biblioteke System.Threading, uključujući dostupne mehanizme za sinhronizaciju i 
	zaključavanje. Dozvoljeno je korišćenje ThreadPool-a. 

Preporuka: 
	Za testiranje API-a je moguće koristiti Postman alat.

Zadatak 12:
	Kreirati Web server koji vrši kriptovanje fajla. Za proces kriptovanja se može koristiti SHA256 iz 
	System.Security.Cryptography.Alterantivno, moguće je koristiti SharpHash biblioteku (biblioteku 
	je moguće instalirati korišćenjem NuGet package managera).Svi zahtevi serveru se šalju preko 
	browser-a korišćenjem GET metode. U zahtevu se kao parametar navodi naziv fajla. Server 
	prihvata zahtev, pretražuje root folder za zahtevani fajl i vrši kriptovanje. Ukoliko traženi fajl ne 
	postoji, vratiti grešku korisniku.
	Primer poziva serveru: http://localhost:5050/fajl.txt